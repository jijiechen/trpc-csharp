using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using TrpcSharp.Protocol.Framing.MessageCodecs;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing
{
    public class DefaultTrpcPacketFramer : ITrpcPacketFramer
    {
        public bool TryReadMessageAsClient(ReadOnlySequence<byte> buffer, out ITrpcMessage trpcMessage,
            out long dataLength,  out SequencePosition consumed, out SequencePosition examined)
        {
            return TryReadMessageCore(false, buffer, out trpcMessage, out dataLength, out consumed, out examined);
        }

        public bool TryReadMessageAsServer(ReadOnlySequence<byte> buffer, out ITrpcMessage trpcMessage, 
            out long dataLength, out SequencePosition consumed, out SequencePosition examined)
        {
            return TryReadMessageCore(true, buffer, out trpcMessage, out dataLength, out consumed, out examined);
        }

        public bool TryReadMessageCore(bool readAsServer, ReadOnlySequence<byte> buffer, out ITrpcMessage trpcMessage, 
            out long dataLength, out SequencePosition consumed, out SequencePosition examined)
        {
            examined = consumed = buffer.Start;
            dataLength = 0;
            var hasHeader = PacketHeaderCodec.TryDecodePacketHeader(buffer, out var frameHeader);
            if (!hasHeader)
            {
                examined = buffer.End;
                trpcMessage = null;
                return false;
            }

            if (buffer.Length < PacketHeaderPositions.FrameHeader_TotalLength + frameHeader.MessageHeaderSize)
            {
                examined = buffer.End;
                trpcMessage = null;
                return false;
            }

            var messageHeaderBytes = buffer.Slice(
                buffer.GetPosition(PacketHeaderPositions.FrameHeader_TotalLength, buffer.Start),
                frameHeader.MessageHeaderSize);
            dataLength = frameHeader.PacketTotalSize
                         - PacketHeaderPositions.FrameHeader_TotalLength 
                         - frameHeader.MessageHeaderSize;
            
            switch (frameHeader.FrameType)
            {
                case TrpcDataFrameType.TrpcUnaryFrame:
                    trpcMessage = readAsServer
                        ? (ITrpcMessage)UnaryRequestMessageCodec.Decode(frameHeader, messageHeaderBytes)
                        : UnaryResponseMessageCodec.Decode(frameHeader, messageHeaderBytes);
                    examined = consumed = buffer.GetPosition(frameHeader.PacketTotalSize, buffer.Start);
                    return true;
                case TrpcDataFrameType.TrpcStreamFrame:
                    trpcMessage = StreamMessageCodec.Decode(frameHeader, messageHeaderBytes);
                    examined = consumed = buffer.GetPosition(frameHeader.PacketTotalSize, buffer.Start);
                    return true;
                default:
                    throw new InvalidDataException($"Unsupported tRPC frame type: {frameHeader.FrameType}");
            }
        }

        public async Task WriteMessageAsync(ITrpcMessage reqMessage, Stream output)
        {
            switch (reqMessage)
            {
                case UnaryRequestMessage unaryReqMsg:
                    await UnaryRequestMessageCodec.EncodeAsync(unaryReqMsg, PacketHeaderCodec.EncodePacketHeader, output);
                    return;
                case UnaryResponseMessage unaryRespMsg:
                    await UnaryResponseMessageCodec.EncodeAsync(unaryRespMsg, PacketHeaderCodec.EncodePacketHeader, output);
                    return;
                case StreamMessage streamMsg:
                    await StreamMessageCodec.EncodeAsync(streamMsg,  PacketHeaderCodec.EncodePacketHeader, output);
                    return;
                default:
                    throw new InvalidDataException($"Unsupported tRPC message type: {reqMessage.GetType()}");
            }
        }
    }
}