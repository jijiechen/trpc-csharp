using System;
using System.Buffers;
using System.IO;
using TrpcSharp.Protocol.Framing.MessageCodecs;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing
{
    public class DefaultTrpcPacketFramer : ITrpcPacketFramer
    {
        public bool TryReadMessageAsClient(ref ReadOnlySequence<byte> buffer, out ITrpcMessage trpcMessage, out SequencePosition consumed,
            out SequencePosition examined)
        {
            return TryReadMessageCore(false, ref buffer, out trpcMessage, out consumed, out examined);
        }

        public bool TryReadMessageAsServer(ref ReadOnlySequence<byte> buffer, out ITrpcMessage trpcMessage, out SequencePosition consumed,
            out SequencePosition examined)
        {
            return TryReadMessageCore(true, ref buffer, out trpcMessage, out consumed, out examined);
        }

        public bool TryReadMessageCore(bool readAsServer, ref ReadOnlySequence<byte> buffer, out ITrpcMessage trpcMessage, 
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
                    trpcMessage = readAsServer
                        ? (ITrpcMessage)UnaryRequestMessageCodec.Decode(frameHeader, messageBytes)
                        : UnaryResponseMessageCodec.Decode(frameHeader, messageBytes);
                    examined = consumed = buffer.GetPosition(frameHeader.PacketTotalSize, buffer.Start);
                    return true;
                case TrpcDataFrameType.TrpcStreamFrame:
                    trpcMessage = StreamMessageCodec.Decode(frameHeader, messageBytes);
                    examined = consumed = buffer.GetPosition(frameHeader.PacketTotalSize, buffer.Start);
                    return true;
                default:
                    throw new InvalidDataException($"Unsupported tRPC frame type: {frameHeader.FrameType}");
            }
        }

        public void WriteMessage(ITrpcMessage reqMessage, IBufferWriter<byte> output)
        {
            switch (reqMessage)
            {
                case UnaryRequestMessage unaryReqMsg:
                    UnaryRequestMessageCodec.Encode(unaryReqMsg, PacketHeaderCodec.EncodePacketHeader, output);
                    break;
                case UnaryResponseMessage unaryRespMsg:
                    UnaryResponseMessageCodec.Encode(unaryRespMsg, PacketHeaderCodec.EncodePacketHeader, output);
                    break;
                case StreamMessage streamMsg:
                    StreamMessageCodec.Encode(streamMsg, PacketHeaderCodec.EncodePacketHeader, output);
                    break;
                default:
                    throw new InvalidDataException($"Unsupported tRPC message type: {reqMessage.GetType()}");
            }
        }
    }
}