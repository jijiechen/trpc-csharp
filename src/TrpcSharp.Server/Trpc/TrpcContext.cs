using System.IO.Pipelines;
using TrpcSharp.Protocol;

namespace TrpcSharp.Server.Trpc
{
    public class TrpcContext
    {
        public StreamMessage StreamMessage { get; set; }
        
        public UnaryRequestMessage UnaryRequest { get; set; }
        
        public UnaryResponseMessage UnaryResponse { get; set; }
        
        public bool IsHandled { get; set; }
        
        public ContextId Id { get; set; }
        public IDuplexPipe Transport { get; set; }
    }

    public struct ContextId
    {
        public ContextType Type { get; set; }
        public uint Id { get; set; }
        
        public override string ToString()
        {
            var prefix = Type == ContextType.UnaryRequest ? "unary-" : "stream-";
            return $"{prefix}-{this.Id}";
        }
    }
    
    
    public enum ContextType
    {
        UnaryRequest = 0,
        StreamConnection = 1,
    }
}