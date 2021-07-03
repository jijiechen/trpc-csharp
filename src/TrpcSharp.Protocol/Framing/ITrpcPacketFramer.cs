﻿using System;
using System.Buffers;

namespace TrpcSharp.Protocol.Framing
{
    public interface ITrpcPacketFramer
    {
        bool TryReadMessageAsClient(ref ReadOnlySequence<byte> buffer, out ITrpcMessage trpcMessage,
            out SequencePosition consumed, out SequencePosition examined);
        
        bool TryReadMessageAsServer(ref ReadOnlySequence<byte> buffer, out ITrpcMessage trpcMessage,
            out SequencePosition consumed, out SequencePosition examined);
        
        void WriteMessage(ITrpcMessage trpcMessage, IBufferWriter<byte> output);
    }
}