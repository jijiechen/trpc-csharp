using System;

namespace TrpcSharp.Server.Exceptions
{
    public class WindowSizeExceededException: Exception
    {
        public WindowSizeExceededException(long dataLength, uint windowSize)
        : base($"Trying to send {dataLength} bytes exceeds the remaining window size {windowSize}")
        {
            
        } 
    }
}