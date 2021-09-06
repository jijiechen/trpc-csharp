using System;

namespace TrpcSharp.Server.Exceptions
{
    public class TrpcCompletedException: Exception
    {
        public TrpcCompletedException(string reason)
            : base($"tRPC call terminated, because of '{reason}'")
        {
            
        }
        
        public TrpcCompletedException(Exception error)
            : base($"tRPC call terminated by application due to an error", error)
        {
            
        }
        
        public TrpcCompletedException()
            : base($"tRPC call terminated by application")
        {
            
        }

    }
}