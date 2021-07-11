﻿using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using TrpcSharp.Protocol.Framing.MessageCodecs;
using TrpcSharp.Protocol.IO;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing
{
    public class DefaultTrpcPacketFramer : ITrpcPacketFramer
    {
        public bool TryReadMessageAsClient(ReadOnlySequence<byte> buffer, out ITrpcMessage trpcMessage,
            out SequencePosition consumed, out SequencePosition examined)
        {
            return TryReadMessageCore(false, buffer, out trpcMessage, out consumed, out examined);
        }

        public bool TryReadMessageAsServer(ReadOnlySequence<byte> buffer, out ITrpcMessage trpcMessage, 
            out SequencePosition consumed, out SequencePosition examined)
        {
            return TryReadMessageCore(true, buffer, out trpcMessage, out consumed, out examined);
        }

        public bool TryReadMessageCore(bool readAsServer, ReadOnlySequence<byte> buffer, out ITrpcMessage trpcMessage, 
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

            var messageStart = buffer.GetPosition(PacketHeaderPositions.FrameHeader_TotalLength, buffer.Start);
            var messageBytes = buffer.Slice(messageStart, 
                frameHeader.PacketTotalSize - PacketHeaderPositions.FrameHeader_TotalLength);
            var messageBuffer = messageBytes.CopySequence();
            
            switch (frameHeader.FrameType)
            {
                case TrpcDataFrameType.TrpcUnaryFrame:
                    trpcMessage = readAsServer
                        ? (ITrpcMessage)UnaryRequestMessageCodec.Decode(frameHeader, messageBuffer)
                        : UnaryResponseMessageCodec.Decode(frameHeader, messageBuffer);
                    examined = consumed = buffer.GetPosition(frameHeader.PacketTotalSize, buffer.Start);
                    return true;
                case TrpcDataFrameType.TrpcStreamFrame:
                    trpcMessage = StreamMessageCodec.Decode(frameHeader, messageBuffer);
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