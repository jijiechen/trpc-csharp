using System;
using System.Buffers;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing
{
    public interface ITrpcPacketFramer
    {
        bool TryParseMessage(ref ReadOnlySequence<byte> buffer, out ITrpcRequestMessage trpcMessage, out SequencePosition consumed, out SequencePosition examined);
    }
    
    public interface ITrpcRequestMessage
    { }
}