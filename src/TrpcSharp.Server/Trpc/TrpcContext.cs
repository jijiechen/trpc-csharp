using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;
using TrpcSharp.Protocol;
using TrpcSharp.Protocol.Framing;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Server.Trpc
{
    public abstract class TrpcContext
    {
        public ContextId Id { get; set; }
        public IDuplexPipe Transport { get; set; }
    }

    public class UnaryTrpcContext : TrpcContext
    {       
        private readonly ITrpcPacketFramer _framer;
        private volatile bool _hasResponded = false;
        internal UnaryTrpcContext(ITrpcPacketFramer framer)
        {
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
            
            await _framer.WriteMessage(UnaryResponse, Transport.Output.AsStream(leaveOpen: true));
        }
    }
    
    public class StreamTrpcContext: TrpcContext
    {  
        private readonly ITrpcPacketFramer _framer;
        internal StreamTrpcContext(ITrpcPacketFramer framer)
        {
            _framer = framer;
        }
        public StreamMessage StreamMessage { get; set; }

        public async Task Push(Stream data)
        {
            var streamMessage = new StreamDataMessage
            {
                StreamId = StreamMessage.StreamId,
                Data = data
            };
            
            await _framer.WriteMessage(streamMessage, Transport.Output.AsStream(leaveOpen: true));
        }
    }


    public struct ContextId
    {
        public ContextType Type { get; set; }
        public uint Id { get; set; }
        
        public override string ToString()
        {
            var prefix = Type == ContextType.UnaryRequest ? "unary" : "stream";
            return $"{prefix}-{this.Id}";
        }
    }
    
    
    public enum ContextType
    {
        UnaryRequest = 0,
        StreamConnection = 1,
    }
}