using System;
using System.Buffers;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing
{
    public interface ITrpcMessageFramer
    {
        bool TryParseMessage(ref ReadOnlySequence<byte> buffer, out UnaryRequestMessage trpcMessage, out SequencePosition consumed, out SequencePosition examined);
    }
}