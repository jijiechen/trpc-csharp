using System;
using System.Buffers;
using TrpcSharp.Protocol.Framing.MessageFramers;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing
{
    public class DefaultTrpcPacketFramer : ITrpcPacketFramer
    {
        public bool TryReadRequestMessage(ref ReadOnlySequence<byte> buffer, out ITrpcRequestMessage trpcMessage, 
            out SequencePosition consumed, out SequencePosition examined)
        {
            examined = consumed = buffer.Start;
            var hasHeader = PacketHeaderCodec.TryDecodePacketHeader(buffer, out var frameHeader);
            if (!hasHeader)
            {
                examined = buffer.End;
                trpcMessage = null;
                return false;
            }

            if (buffer.Length < frameHeader.PacketTotalSize)
            {
                examined = buffer.End;
                trpcMessage = null;
                return false;
            }

            var messageBytes = buffer.Slice(PacketHeaderPositions.FrameHeader_TotalLength);
            switch (frameHeader.FrameType)
            {
                case TrpcDataFrameType.TrpcUnaryFrame:
                    trpcMessage = UnaryMessageFramer.DecodeRequestMessage(frameHeader, messageBytes);
                    examined = consumed = buffer.GetPosition(frameHeader.PacketTotalSize, buffer.Start);
                    return true;
                case TrpcDataFrameType.TrpcStreamFrame:
                    trpcMessage = StreamMessageFramer.Decode(frameHeader, messageBytes);
                    examined = consumed = buffer.GetPosition(frameHeader.PacketTotalSize, buffer.Start);
                    return true;
                default:
                    // should not reach here!
                    examined = buffer.End;
                    trpcMessage = null;
                    return false;
            }
        }

        public void WriteRequestMessage(ITrpcRequestMessage reqMessage, IBufferWriter<byte> output)
        {
            if (reqMessage is UnaryRequestMessage unaryMsg)
            {
                UnaryMessageFramer.EncodeRequestMessage(unaryMsg, PacketHeaderCodec.EncodePacketHeader, output);
            }
            
            if (reqMessage is StreamMessage streamMsg)
            {
                StreamMessageFramer.Encode(streamMsg, PacketHeaderCodec.EncodePacketHeader, output);
            }
        }

        public bool TryReadResponseMessage(ref ReadOnlySequence<byte> buffer, out ITrpcResponseMessage trpcMessage,
            out SequencePosition consumed, out SequencePosition examined)
        {
            throw new NotImplementedException();
        }

        public void WriteResponseMessage(ITrpcResponseMessage respMessage, IBufferWriter<byte> output)
        {
            throw new NotImplementedException();
        }
    }
}