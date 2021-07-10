using System;
using System.IO;
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
        public UnaryTrpcContext(uint requestId, ITrpcPacketFramer framer)
        {
            Identifier = new ContextId{ Type = ContextType.UnaryRequest, Id =  requestId};
            _framer = framer;
        }

        public bool HasResponded => _hasResponded;
        public UnaryRequestMessage UnaryRequest { get; set; }
        
        public UnaryResponseMessage UnaryResponse { get; set; }
        
        public async Task RespondAsync()
        {
            if (_hasResponded || UnaryRequest.CallType == TrpcCallType.TrpcOnewayCall)
            {
                return;
            }
            
            _hasResponded = true;
            await _framer.WriteMessageAsync(UnaryResponse, Connection.Transport.Output.AsStream(leaveOpen: true));
        }
    }
    
    public class StreamTrpcContext: TrpcContext, IStreamCallTracker
    {
        private volatile uint _clientWindowSize;
        private volatile uint _windowSize;
        private CancellationTokenSource _windowSizeWaitHandle;
        private readonly ITrpcPacketFramer _framer;
        private TrpcRetCode? _initResponseCode = null;
        public StreamTrpcContext(uint streamId, ITrpcPacketFramer framer)
        {
            Identifier = new ContextId{ Type = ContextType.Streaming, Id =  streamId};
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
                streamingMode == TrpcServerStreamingMode.ServerStreaming)
            {
                ReceiveChannel = Channel.CreateUnbounded<Stream>();
            }

            if (streamingMode == TrpcServerStreamingMode.DuplexStreaming ||
                streamingMode == TrpcServerStreamingMode.ClientStreaming)
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

        public async Task FlushAllAsync(int secondsWaitForWindowSize = 60)
        {
            if (!AssertInitSuccess(throwIfFailed: false))
            {
                return;
            }
            
            var channelReader = SendChannel.Reader;
            await foreach (var item in channelReader.ReadAllAsync().ConfigureAwait(false))
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
                
                await WriteToOutputAsync(item).ConfigureAwait(false);
            }
        }

        private async Task WriteToOutputAsync(Stream data)
        {
            var streamMessage = new StreamDataMessage
            {
                StreamId = Identifier.Id,
                Data = data
            };
            
            await _framer.WriteMessageAsync(streamMessage, Connection.Transport.Output.AsStream(leaveOpen: true));
            await data.DisposeAsync();

            WindowSizeUsed((uint) data.Length);
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
            
            await _framer.WriteMessageAsync(trpcMessage, Connection.Transport.Output.AsStream(leaveOpen: true));
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

        async Task IStreamCallTracker.RespondInitMessageAsync(TrpcRetCode retCode)
        {
            await RespondInitAsync(retCode);
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
        }

        void IStreamCallTracker.IncrementSendWindowSize(uint streamId, uint increment)
        {
            if (streamId != Identifier.Id)
            {
                return;
            }
            
            Interlocked.Add(ref _windowSize, increment);
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
    }

    public interface IStreamCallTracker
    {
        Task RespondInitMessageAsync(TrpcRetCode retCode);
        void IncrementSendWindowSize(uint streamId, uint increment);
        void MarkWindowSizeAsSent(uint streamId, uint windowSize);
        Task FeedbackReadWindowSizeAsync(uint streamId, uint windowSize);
    }
    
    public enum TrpcServerStreamingMode
    {
        ClientStreaming,
        ServerStreaming,
        DuplexStreaming
    }

    public struct ContextId
    {
        public ContextType Type { get; set; }
        public uint Id { get; set; }
        
        public override string ToString()
        {
            var prefix = Type == ContextType.Streaming ? "stream" : "unary";
            return $"{prefix}-{this.Id}";
        }
    }

    public enum ContextType
    {
        UnaryRequest = 0,
        Streaming = 1,
    }
}