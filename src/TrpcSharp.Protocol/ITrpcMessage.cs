using System.IO;

namespace TrpcSharp.Protocol
{
    public interface ITrpcMessage
    {
        void SetMessageData(Stream stream);
    }
}