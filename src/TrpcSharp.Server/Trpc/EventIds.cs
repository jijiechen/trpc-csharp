using Microsoft.Extensions.Logging;

namespace TrpcSharp.Server.Trpc
{
    public static class EventIds
    {
        // 91xx: Connection Events
        public static readonly EventId ConnectionReset = new EventId(9100, "Connection Reset");
        public static readonly EventId UnknownConnectionError = new EventId(9101, "Connection Error");
        
        // 94xx: Format Events
        public static readonly EventId ProtocolError = new EventId(9400, "Protocol Error");
        
        // 95xx: Business Errors
        public static readonly EventId ApplicationError = new EventId(9500, "Application Error");
        
    }
}