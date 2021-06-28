using System.Collections.Generic;

namespace TrpcSharp.Server.Trpc
{
    public interface ITrpcMessage
    {
        uint Version { get; set; }
        uint CallType { get; set; }
        uint RequestId { get; set; }
        uint Timeout { get; set; }
        byte[] Caller { get; set; }
        byte[] Callee { get; set; }
        byte[] Func { get; set; }
        uint MessageType { get; set; }
        Dictionary<string, byte[]> TransInfo { get; set; }
        uint ContentType { get; set; }
        uint ContentEncoding { get; set; }
    }
}