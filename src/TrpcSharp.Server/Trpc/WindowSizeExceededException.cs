using System;

namespace TrpcSharp.Server.Trpc
{
    public class WindowSizeExceededException: Exception
    {
        public WindowSizeExceededException(long dataLength, uint windowSize)
        : base($"Trying to send {dataLength} bytes exceeds the window size left {windowSize}")
        {
            
        } 
    }
}