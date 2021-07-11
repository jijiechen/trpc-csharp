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
        public static readonly EventId StreamIdNotFound = new EventId(9420, "Stream Id Not Found");
        public static readonly EventId StreamIdDuplicated = new EventId(9421, "Duplicated Stream Id Detected");
        
        // 95xx: Business Errors
        public static readonly EventId ApplicationError = new EventId(9500, "Application Error");
        
        // 92xx: Normal events
        public static readonly EventId UnaryRequestReceived = new EventId(9200, "Unary Request Received");
        public static readonly EventId StreamInitialization = new EventId(9201, "Stream Session Initializing");
        public static readonly EventId StreamDataReceived = new EventId(9206, "Stream Data Received");
        public static readonly EventId StreamFeedbackReceived = new EventId(9207, "Stream Feedback Received");
        public static readonly EventId StreamComplete = new EventId(9208, "Stream Session Initializing");
        public static readonly EventId StreamCloseReceived = new EventId(9209, "Stream Close Received");
        
    }
}