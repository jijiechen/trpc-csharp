using System;

namespace TrpcSharp.Server
{
    public class WindowSizeExceededException: Exception
    {
        public WindowSizeExceededException(long dataLength, uint windowSize)
        : base($"Trying to send {dataLength} bytes exceeds the remaining window size {windowSize}")
        {
            
        } 
    }
}