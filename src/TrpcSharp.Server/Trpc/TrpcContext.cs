using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;
using TrpcSharp.Protocol;
using TrpcSharp.Protocol.Framing;

namespace TrpcSharp.Server.Trpc
{
    public abstract class TrpcContext
    {
        public ContextId Id { get; set; }
        public IDuplexPipe Transport { get; set; }
    }

    public class UnaryTrpcContext : TrpcContext
    {
        public UnaryRequestMessage UnaryRequest { get; set; }
        
        public UnaryResponseMessage UnaryResponse { get; set; }
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
            
            _framer.WriteMessage(streamMessage, Transport.Output.AsStream(leaveOpen: true));
            await Transport.Output.FlushAsync();
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