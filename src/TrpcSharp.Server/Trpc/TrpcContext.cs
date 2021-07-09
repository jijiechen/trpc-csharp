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
        
        public async Task Respond()
        {
            _hasResponded = true;
            if (UnaryRequest.CallType == TrpcCallType.TrpcOnewayCall)
            {
                return;
            }
            
            await _framer.WriteMessageAsync(UnaryResponse, Connection.Transport.Output.AsStream(leaveOpen: true));
        }
    }
    
    public class StreamTrpcContext: TrpcContext, IStreamWindowSizeCounter
    {
        private volatile uint _windowSize;
        private volatile uint _clientWindowSize;
        private readonly ITrpcPacketFramer _framer;
        public StreamTrpcContext(uint streamId, ITrpcPacketFramer framer)
        {
            Identifier = new ContextId{ Type = ContextType.Streaming, Id =  streamId};
            _framer = framer;
        }
        public StreamMessage StreamMessage { get; set; }

        public void InitializeDuplexStreamChannels()
        {
            if (!(StreamMessage is StreamInitMessage initMessage))
            {
                throw new InvalidOperationException("DuplexStreamChannels are only available on tRPC Init contexts");
            }
            
            ReceiveChannel = Channel.CreateUnbounded<Stream>();
            SendChannel = Channel.CreateUnbounded<Stream>();

            _windowSize = initMessage.InitWindowSize;
            _clientWindowSize = initMessage.InitWindowSize;
        }
        
        public Channel<Stream> SendChannel { get; set; }
        
        public Channel<Stream> ReceiveChannel { get; set; }

        public async Task FlushAllAsync(int secondsWaitForWindowSize = 60)
        {
            var channelReader = SendChannel.Reader;
            await foreach (var item in channelReader.ReadAllAsync().ConfigureAwait(false))
            {
                var waited = false;
                while (item.Length > _windowSize)
                {
                    if (!waited && secondsWaitForWindowSize > 0)
                    {
                        await Task.Delay(secondsWaitForWindowSize);
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

        private void WindowSizeUsed(uint usedWindowSize)
        {
            Interlocked.Exchange(ref _windowSize, _windowSize - usedWindowSize);
        }

        void IStreamWindowSizeCounter.IncrementSendWindowSize(uint streamId, uint increment)
        {
            if (streamId != Identifier.Id)
            {
                return;
            }
            
            Interlocked.Add(ref _windowSize, increment);
        }

        void IStreamWindowSizeCounter.MarkWindowSizeAsSent(uint streamId, uint usedWindowSize)
        {
            if (streamId != Identifier.Id)
            {
                return;
            }

            WindowSizeUsed(usedWindowSize);
        }

        public async Task FeedbackReadWindowSizeAsync(uint streamId, uint windowSize)
        {
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

    public interface IStreamWindowSizeCounter
    {
        void IncrementSendWindowSize(uint streamId, uint increment);
        void MarkWindowSizeAsSent(uint streamId, uint windowSize);
        Task FeedbackReadWindowSizeAsync(uint streamId, uint windowSize);
    }
    
    public enum TrpcStreamingMode
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