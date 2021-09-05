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

            var allHeaderSize = CalcMessageHeadersSize(frameHeader);
            if (buffer.Length < allHeaderSize)
            {
                examined = buffer.End;
                trpcMessage = null;
                return false;
            }

            var messageHeaderBytes = buffer.Slice(
                buffer.GetPosition(PacketHeaderPositions.FrameHeader_TotalLength, buffer.Start),
                allHeaderSize - PacketHeaderPositions.FrameHeader_TotalLength);
            dataLength = frameHeader.PacketTotalSize - allHeaderSize;
            
            switch (frameHeader.FrameType)
            {
                case TrpcDataFrameType.TrpcUnaryFrame:
                    trpcMessage = readAsServer
                        ? (ITrpcMessage)UnaryRequestMessageCodec.Decode(frameHeader, messageHeaderBytes)
                        : UnaryResponseMessageCodec.Decode(frameHeader, messageHeaderBytes);
                    examined = consumed = buffer.GetPosition(allHeaderSize, buffer.Start);
                    return true;
                case TrpcDataFrameType.TrpcStreamFrame:
                    trpcMessage = StreamMessageCodec.Decode(frameHeader, messageHeaderBytes);
                    examined = consumed = buffer.GetPosition(allHeaderSize, buffer.Start);
                    return true;
                default:
                    throw new InvalidDataException($"Unsupported tRPC frame type: {frameHeader.FrameType}");
            }
        }

        private static int CalcMessageHeadersSize(PacketHeader frameHeader)
        {
            return frameHeader.FrameType switch
            {
                TrpcDataFrameType.TrpcUnaryFrame 
                    => PacketHeaderPositions.FrameHeader_TotalLength + frameHeader.MessageHeaderSize,
                
                // stream frame
                TrpcDataFrameType.TrpcStreamFrame when frameHeader.StreamFrameType == TrpcStreamFrameType.TrpcStreamFrameData 
                    => PacketHeaderPositions.FrameHeader_TotalLength,
                
                TrpcDataFrameType.TrpcStreamFrame when frameHeader.PacketTotalSize > int.MaxValue
                    => throw new InvalidDataException("Message too large"),
                
                TrpcDataFrameType.TrpcStreamFrame 
                    => (int)frameHeader.PacketTotalSize,
                
                _ => throw new InvalidDataException($"Unsupported tRPC frame type: {frameHeader.FrameType}")
            };
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