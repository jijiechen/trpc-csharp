using System;
using System.IO;
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
    
    public class StreamTrpcContext: TrpcContext
    {
        private uint _windowSize;
        private readonly ITrpcPacketFramer _framer;
        public StreamTrpcContext(uint streamId, ITrpcPacketFramer framer)
        {
            Identifier = new ContextId{ Type = ContextType.Streaming, Id =  streamId};
            _framer = framer;
        }
        public StreamMessage StreamMessage { get; set; }

        public void IncrementWindowSize(uint increment)
        {
            _windowSize += increment;
        }

        public async Task<bool> WriteAsync(Stream data)
        {
            if (data.Length > _windowSize)
            {
                return false;
            }
            
            var streamMessage = new StreamDataMessage
            {
                StreamId = Identifier.Id,
                Data = data
            };
            
            await _framer.WriteMessageAsync(streamMessage, Connection.Transport.Output.AsStream(leaveOpen: true));
            _windowSize -= (uint)data.Length;
            return _windowSize > 0;
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