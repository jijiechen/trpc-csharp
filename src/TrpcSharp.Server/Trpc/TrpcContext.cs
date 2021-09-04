using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TrpcSharp.Protocol;
using TrpcSharp.Protocol.Framing;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Server.Trpc
{
    public abstract class TrpcContext
    {
        public ContextId Identifier { get; protected set; }
        public IConnection Connection { get; set; }
        public IServiceProvider Services { get; set; }
        
        public override string ToString()
        {
            return Identifier.ToString();
        }
    }

    public class UnaryTrpcContext : TrpcContext
    {       
        private readonly ITrpcPacketFramer _framer;
        private volatile bool _hasResponded = false;
        public UnaryTrpcContext(string connId, uint requestId, ITrpcPacketFramer framer)
        {
            Identifier = new ContextId{ Type = ContextType.UnaryRequest, Id =  requestId, ConnectionId = connId};
            _framer = framer;
        }

        public bool HasResponded => _hasResponded;
        public UnaryRequestMessage Request { get; set; }
        
        public UnaryResponseMessage Response { get; set; }
        
        public async Task RespondAsync()
        {
            if (_hasResponded || Request.CallType == TrpcCallType.TrpcOnewayCall)
            {
                return;
            }
            
            _hasResponded = true;
            await _framer.WriteMessageAsync(Response, Connection.Transport.Output.AsStream(leaveOpen: true));
        }
    }
    
    public class StreamTrpcContext: TrpcContext, IStreamCallTracker
    {
        private volatile uint _clientWindowSize;
        private volatile uint _windowSize;
        private CancellationTokenSource _windowSizeWaitHandle;
        private readonly ITrpcPacketFramer _framer;
        private TrpcRetCode? _initResponseCode = null;
        public StreamTrpcContext(string connId, uint streamId, ITrpcPacketFramer framer)
        {
            Identifier = new ContextId{ Type = ContextType.Streaming, Id =  streamId, ConnectionId = connId};
            _framer = framer;
        }
        public StreamMessage StreamMessage { get; set; }
        
        public TrpcServerStreamingMode? StreamingMode { get; set; }

        public async Task InitializeStreamingAsync(TrpcServerStreamingMode streamingMode)
        {
            if (_initResponseCode != null)
            {
                throw new InvalidOperationException("DuplexStreamChannels are only available on tRPC Init contexts");
            }
            
            if (!(StreamMessage is StreamInitMessage initMessage))
            {
                throw new InvalidOperationException("DuplexStreamChannels are only available on tRPC Init contexts");
            }

            if (streamingMode == TrpcServerStreamingMode.DuplexStreaming ||
                streamingMode == TrpcServerStreamingMode.ClientStreaming)
            {
                ReceiveChannel = Channel.CreateUnbounded<Stream>();
            }

            if (streamingMode == TrpcServerStreamingMode.DuplexStreaming ||
                streamingMode == TrpcServerStreamingMode.ServerStreaming)
            {
                SendChannel = Channel.CreateUnbounded<Stream>();
            }

            initMessage.InitWindowSize = initMessage!.InitWindowSize < 1
                ? StreamInitMessage.DefaultWindowSize
                : initMessage!.InitWindowSize;
            _windowSize = initMessage.InitWindowSize;
            _clientWindowSize = initMessage.InitWindowSize;

            StreamingMode = streamingMode;
            await RespondInitAsync(TrpcRetCode.TrpcInvokeSuccess);
        }

        public Channel<Stream> SendChannel { get; set; }

        public Channel<Stream> ReceiveChannel { get; set; }

        public void WriteComplete()
        {
            SendChannel?.Writer.TryComplete();
        }
        
        public async IAsyncEnumerable<Stream> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (ReceiveChannel?.Reader == null)
            {
                yield break;
            }
            
            await foreach(var s in ReceiveChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return s;
            };
        }
        
        public async Task FlushAllAsync(int secondsWaitForWindowSize = 60)
        {
            if (!AssertInitSuccess(throwIfFailed: false))
            {
                return;
            }
            
            var sender = SendChannel?.Reader;
            if (sender == null)
            {
                return;
            }
            
            await foreach (var item in sender.ReadAllAsync().ConfigureAwait(false))
            {
                var waited = false;
                while (item.Length > _windowSize)
                {
                    if (!waited && secondsWaitForWindowSize > 0)
                    {
                        var cts = new CancellationTokenSource();
                        _windowSizeWaitHandle = cts;
                        await Task.Delay(secondsWaitForWindowSize, cts.Token);
                        _windowSizeWaitHandle?.Dispose();
                        _windowSizeWaitHandle = null;
                        waited = true;
                    }
                    else
                    {
                        throw new WindowSizeExceededException(item.Length, _windowSize);
                    }
                }
                
                var streamMessage = new StreamDataMessage
                {
                    StreamId = Identifier.Id,
                    Data = item
                };
                await WriteMessageToOutput(streamMessage).ConfigureAwait(false);
            }
        }

        public async Task WriteAsync(Stream stream)
        {
            AssertInitSuccess(throwIfFailed: true);
            
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            await SendChannel.Writer.WriteAsync(stream);
        }

        public async Task WriteAsync(StreamMessage trpcMessage)
        {
            if (trpcMessage is StreamDataMessage)
            {
                throw new InvalidOperationException(
                    $"Please use {nameof(WriteAsync)}(Stream) overload to write this data");
            }

            await WriteMessageToOutput(trpcMessage);
        }

        private async Task WriteMessageToOutput(StreamMessage trpcMessage)
        {
            var dataMessage = trpcMessage as StreamDataMessage;
            uint dataLength = 0;
            if (dataMessage != null)
            {
                dataLength = (uint) (dataMessage.Data?.Length ?? 0);
            }

            try
            {
                if (Connection == null)
                {
                    return;
                }
                
                await _framer.WriteMessageAsync(trpcMessage, Connection.Transport.Output.AsStream(leaveOpen: true));
                if (dataLength > 0)
                {
                    WindowSizeUsed(dataLength);
                }
            }
            catch (InvalidOperationException)
            {
                // the Pipe Output is complete 
            }
            finally
            {
                try
                {
                    if (dataMessage?.Data != null)
                    {
                        await dataMessage.Data.DisposeAsync();
                    }
                }
                catch
                {
                    // we can't do anything if the dispose fails
                }
            }
        }

        private bool AssertInitSuccess(bool throwIfFailed)
        {
            if (_initResponseCode == TrpcRetCode.TrpcInvokeSuccess) return true;

            if (_initResponseCode != null)
            {
                if(throwIfFailed)
                {
                    throw new InvalidOperationException(
                        $"The tRPC stream {Identifier.Id} invocation has not been initialized.");
                }
            }
            else
            {
                if(throwIfFailed)
                {
                    throw new InvalidOperationException(
                        $"The tRPC stream {Identifier.Id} invocation has been marked as {_initResponseCode}.");
                }
            }

            return false;
        }

        #region IStreamCallTracker

        async Task IStreamCallTracker.RespondInitMessageAsync(uint streamId, TrpcRetCode retCode)
        {
            if (streamId != this.Identifier.Id)
            {
                return;
            }
            
            await RespondInitAsync(retCode);
        }

        private TaskCompletionSource _responseTcs; 
        Task IStreamCallTracker.GetInitResponseTask(uint streamId)
        {
            if (streamId != Identifier.Id)
            {
                return Task.CompletedTask;
            }

            if (_responseTcs == null && StreamMessage?.StreamFrameType == TrpcStreamFrameType.TrpcStreamFrameInit)
            {
                _responseTcs = new TaskCompletionSource();
            }

            return _responseTcs?.Task;
        }

        private Func<StreamTrpcContext, TrpcStreamCloseType, Task> _completeHandler; 
        void IStreamCallTracker.OnComplete(Func<StreamTrpcContext, TrpcStreamCloseType, Task> handler)
        {
            _completeHandler = handler;
        }

        async Task IStreamCallTracker.CompleteAsync(uint streamId, TrpcStreamCloseType closeType)
        {
            if (streamId != Identifier.Id)
            {
                return;
            }

            if (_completeHandler != null)
            {
                await _completeHandler(this, closeType);
                _completeHandler = null;
            }
        }

        private async Task RespondInitAsync(TrpcRetCode retCode)
        {
            _initResponseCode = retCode;
            var responseMsg = new StreamInitMessage
            {
                StreamId = Identifier.Id,
                ContentType = TrpcContentEncodeType.TrpcProtoEncode,
                ContentEncoding = TrpcCompressType.TrpcDefaultCompress,
                InitWindowSize = _windowSize,
                ResponseMeta = new StreamInitResponseMeta
                {
                    ReturnCode = retCode
                }
            };
            await WriteAsync(responseMsg);
            _responseTcs?.SetResult();
        }

        void IStreamCallTracker.IncrementSendWindowSize(uint streamId, uint increment)
        {
            if (streamId != Identifier.Id)
            {
                return;
            }
            
            if((long)_windowSize + (long)increment > uint.MaxValue){
                return;
            }

            Interlocked.Add(ref _windowSize, increment);
            if(_windowSize == 0 || _windowSize == uint.MaxValue){
                Interlocked.Exchange(ref _windowSize, StreamInitMessage.DefaultWindowSize);
            }
            _windowSizeWaitHandle?.Cancel();
        }

        void IStreamCallTracker.MarkWindowSizeAsSent(uint streamId, uint usedWindowSize)
        {
            if (streamId != Identifier.Id)
            {
                return;
            }

            WindowSizeUsed(usedWindowSize);
        }

        private void WindowSizeUsed(uint usedWindowSize)
        {
            Interlocked.Exchange(ref _windowSize, _windowSize - usedWindowSize);
            if(_windowSize == 0 || _windowSize == uint.MaxValue){
                Interlocked.Exchange(ref _windowSize, StreamInitMessage.DefaultWindowSize);
            }
        }

        public async Task FeedbackReadWindowSizeAsync(uint streamId, uint windowSize)
        {
            AssertInitSuccess(throwIfFailed: true);
            
            if (streamId != Identifier.Id)
            {
                return;
            }

            var clientWindowSizeLeft = _clientWindowSize < windowSize ? 0 : _clientWindowSize - windowSize;
            Interlocked.Exchange(ref _clientWindowSize, clientWindowSizeLeft);
            
            if (clientWindowSizeLeft >= Math.Ceiling(0.25 * StreamInitMessage.DefaultWindowSize))
            {
                return;
            }

            var feedbackMessage = new StreamFeedbackMessage
            {
                StreamId = Identifier.Id,
                WindowSizeIncrement = StreamInitMessage.DefaultWindowSize - clientWindowSizeLeft
            };
            await WriteAsync(feedbackMessage).ConfigureAwait(false);
        }
        
        #endregion
    }

    public interface IStreamCallTracker
    {
        Task RespondInitMessageAsync(uint streamId, TrpcRetCode retCode);
        Task GetInitResponseTask(uint streamId);

        void OnComplete(Func<StreamTrpcContext, TrpcStreamCloseType, Task> handler);
        Task CompleteAsync(uint streamId, TrpcStreamCloseType closeType);
        
        void IncrementSendWindowSize(uint streamId, uint increment);
        void MarkWindowSizeAsSent(uint streamId, uint windowSize);
        Task FeedbackReadWindowSizeAsync(uint streamId, uint windowSize);
    }
    
    public enum TrpcServerStreamingMode
    {
        /// <summary>
        /// 客户端持续发送数据的流式调用
        /// </summary>
        ClientStreaming,
        /// <summary>
        /// 服务端持续发送数据的流式调用
        /// </summary>
        ServerStreaming,
        /// <summary>
        /// 双向均可持续发送数据的流式调用
        /// </summary>
        DuplexStreaming
    }

    public struct ContextId
    {
        public ContextType Type { get; set; }
        public uint Id { get; set; }
        
        public string ConnectionId { get; set; }
        
        public override string ToString()
        {
            var prefix = Type == ContextType.Streaming ? "stream" : "unary";
            return $"C_{ConnectionId}-T_{prefix}-ID_{Id}";
        }
    }

    public enum ContextType
    {
        UnaryRequest = 0,
        Streaming = 1,
    }
}