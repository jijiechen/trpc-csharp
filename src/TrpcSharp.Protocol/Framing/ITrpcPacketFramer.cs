using System;
using System.Buffers;

namespace TrpcSharp.Protocol.Framing
{
    public interface ITrpcPacketFramer
    {
        bool TryReadRequestMessage(ref ReadOnlySequence<byte> buffer, out ITrpcRequestMessage trpcMessage, out SequencePosition consumed, out SequencePosition examined);
        void WriteRequestMessage(ITrpcRequestMessage reqMessage, IBufferWriter<byte> output);
        
        bool TryReadResponseMessage(ref ReadOnlySequence<byte> buffer, out ITrpcResponseMessage trpcMessage, out SequencePosition consumed, out SequencePosition examined);
        void WriteResponseMessage(ITrpcResponseMessage trpcMessage, IBufferWriter<byte> output);
    }
    
    public interface ITrpcRequestMessage { }
    
    public interface ITrpcResponseMessage { }
}