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

namespace TrpcSharp.Server
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
            
            var outputStream = Connection.Transport.Output.AsStream(leaveOpen: true);
            await _framer.WriteMessageAsync(Response, outputStream).ConfigureAwait(false);
            await outputStream.FlushAsync().ConfigureAwait(false);
        }
    }
    
    public class StreamTrpcContext: TrpcContext, IStreamCallTracker
    {
        // todo: check if request complete and close channels!
        
        private long _clientWindowSize;
        private long _windowSize;
        private CancellationTokenSource _windowSizeWaitHandle;
        private readonly ITrpcPacketFramer _framer;
        private TrpcRetCode? _initResponseCode = null;
        private Stream _outputStream = null;
        
        public StreamTrpcContext(string connId, uint streamId, ITrpcPacketFramer framer)
        {
            Identifier = new ContextId{ Type = ContextType.Streaming, Id =  streamId, ConnectionId = connId};
            _framer = framer;
        }
        public StreamMessage InitMessage { get; set; }
        
        public TrpcServerStreamingMode? StreamingMode { get; set; }

        public async Task InitializeStreamingAsync(TrpcServerStreamingMode streamingMode)
        {
            if (_initResponseCode != null)
            {
                throw new InvalidOperationException("DuplexStreamChannels are only available on tRPC Init contexts");
            }
            
            if (InitMessage is not StreamInitMessage initMessage)
            {
                throw new InvalidOperationException("DuplexStreamChannels are only available on tRPC Init contexts");
            }

            if (streamingMode == TrpcServerStreamingMode.DuplexStreaming 
                || streamingMode == TrpcServerStreamingMode.ClientStreaming)
            {
                ReceiveChannel = Channel.CreateUnbounded<Stream>();
            }

            if (streamingMode == TrpcServerStreamingMode.DuplexStreaming 
                || streamingMode == TrpcServerStreamingMode.ServerStreaming)
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

        internal Channel<Stream> SendChannel { get; set; }

        internal Channel<Stream> ReceiveChannel { get; set; }

        public void WriteComplete()
        {
            // todo: implement complete async
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
                (s as TrpcDataStream)?.MarkedAsAccessed();
            };
        }
        
        internal async Task SendAsync(int secondsWaitForWindowSize = 60)
        {
            // todo: check errors!
            
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
                        throw new WindowSizeExceededException(item.Length, (uint)_windowSize);
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
        
        public async Task FlushAsync()
        {
            var outputStream = GetOutputStream();
            if (outputStream != null)
            {
                await outputStream.FlushAsync();
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

        internal async Task WriteAsync(StreamMessage trpcMessage)
        {
            if (trpcMessage is StreamDataMessage)
            {
                throw new InvalidOperationException(
                    $"Please use {nameof(WriteAsync)}(Stream) overload to write this data");
            }

            await WriteMessageToOutput(trpcMessage);
        }

        private Stream GetOutputStream()
        {
            if (_outputStream == null)
            {
                return _outputStream = Connection?.Transport?.Output?.AsStream(leaveOpen: true);
            }

            return _outputStream;
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
                
                await _framer.WriteMessageAsync(trpcMessage, GetOutputStream());
                if (dataLength > 0)
                {
                    WindowSizeUsed(dataLength);
                }
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

        private TaskCompletionSource<bool> _responseTcs; 
        Task IStreamCallTracker.GetInitResponseTask(uint streamId)
        {
            if (streamId != Identifier.Id)
            {
                return Task.CompletedTask;
            }

            if (_responseTcs == null && InitMessage?.StreamFrameType == TrpcStreamFrameType.TrpcStreamFrameInit)
            {
                _responseTcs = new TaskCompletionSource<bool>();
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
            
            InitMessage = null;
            Services = null;
            Connection = null;
        }

        private async Task RespondInitAsync(TrpcRetCode retCode)
        {
            _initResponseCode = retCode;
            var responseMsg = new StreamInitMessage
            {
                StreamId = Identifier.Id,
                ContentType = TrpcContentEncodeType.TrpcProtoEncode,
                ContentEncoding = TrpcCompressType.TrpcDefaultCompress,
                InitWindowSize = (uint)_windowSize,
                ResponseMeta = new StreamInitResponseMeta
                {
                    ReturnCode = retCode
                }
            };
            await WriteAsync(responseMsg);
            _responseTcs?.SetResult(true);
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
                WindowSizeIncrement = StreamInitMessage.DefaultWindowSize - (uint)clientWindowSizeLeft
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
            var prefix = Type == ContextType.Streaming ? "s" : "u";
            return $"C_{ConnectionId}-trpc_{prefix}-{Id}";
        }
    }

    public enum ContextType
    {
        UnaryRequest = 0,
        Streaming = 1,
    }
}