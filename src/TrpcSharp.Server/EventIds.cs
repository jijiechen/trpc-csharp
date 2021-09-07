using Microsoft.Extensions.Logging;

namespace TrpcSharp.Server
{
    public static class EventIds
    {
        // 91xx: Connection Events
        public static readonly EventId ConnectionReset = new EventId(9100, "Connection Reset");
        public static readonly EventId UnknownConnectionError = new EventId(9101, "Connection Error");
        public static readonly EventId ErrorDrainingMessageData = new EventId(9102, "Timeout Draining Message Body Data");
        public static readonly EventId ConnectionEstablished = new EventId(9188, "Connection Closed");
        public static readonly EventId ConnectionClose = new EventId(9199, "Connection Closed");
        
        // 94xx: Format Events
        public static readonly EventId ProtocolError = new EventId(9400, "Protocol Error");
        public static readonly EventId StreamIdNotFound = new EventId(9420, "Stream Id Not Found");
        public static readonly EventId StreamIdDuplicated = new EventId(9421, "Duplicated Stream Id Detected");
        
        // 95xx: Business Errors
        public static readonly EventId ApplicationError = new EventId(9500, "Application Error");
        public static readonly EventId ServiceFuncNotFound = new EventId(9504, "Service Or Method Not Found");
        
        // 92xx: Normal events
        public static readonly EventId UnaryRequestReceived = new EventId(9200, "Unary Request Received");
        public static readonly EventId RpcStarting = new EventId(9201, "tRPC Completed");
        public static readonly EventId RpcCompleted = new EventId(9201, "tRPC Completed");
        
        public static readonly EventId StreamInitialization = new EventId(9211, "Stream Session Initializing");
        public static readonly EventId StreamDataReceived = new EventId(9212, "Stream Data Received");
        public static readonly EventId StreamFeedbackReceived = new EventId(9213, "Stream Feedback Received");
        public static readonly EventId StreamCloseReceived = new EventId(9214, "Stream Close Received");
        
    }
}