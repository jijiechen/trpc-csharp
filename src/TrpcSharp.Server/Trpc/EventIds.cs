using Microsoft.Extensions.Logging;

namespace TrpcSharp.Server.Trpc
{
    public static class EventIds
    {
        // 91xx: Connection Events
        public static readonly EventId ConnectionReset = new EventId(9100, "Connection Reset");
        public static readonly EventId UnknownConnectionError = new EventId(9101, "Connection Error");
        public static readonly EventId ConnectionClose = new EventId(9199, "Connection Closed");
        
        // 94xx: Format Events
        public static readonly EventId ProtocolError = new EventId(9400, "Protocol Error");
        
        // 95xx: Business Errors
        public static readonly EventId ApplicationError = new EventId(9500, "Application Error");
        
        // 92xx: Normal events
        public static readonly EventId StreamInitialization = new EventId(9201, "Stream session Initializing");
        public static readonly EventId WindowSizeIncrement = new EventId(9205, "Windows Size Incremented");
        
    }
}