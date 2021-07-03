using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing
{
    internal static class PacketHeaderCodec
    {
        
        public static bool TryDecodePacketHeader(in ReadOnlySequence<byte> buffer, out PacketHeader header)
        {
            if (buffer.Length < PacketHeaderPositions.FrameHeader_TotalLength)
            {   
                header = new PacketHeader();
                return false;
            }

            if (buffer.First.Length >= PacketHeaderPositions.FrameHeader_TotalLength)
            {
                var headerSpan = buffer.First.Span.Slice(0, PacketHeaderPositions.FrameHeader_TotalLength);
                return ReadPacketHeader(headerSpan, out header);
            }
            else
            {
                Span<byte> headerBytes = stackalloc byte[PacketHeaderPositions.FrameHeader_TotalLength];
                
                buffer.Slice(0, PacketHeaderPositions.FrameHeader_TotalLength).CopyTo(headerBytes);
                return ReadPacketHeader(headerBytes, out header);
            }
        }
        
        private static bool ReadPacketHeader(in ReadOnlySpan<byte> headerBytes, out PacketHeader header)
        {
            header = new PacketHeader
            {
                Magic = ReadTrpcMagic(headerBytes),
                FrameType = ReadFrameType(headerBytes),
                StreamFrameType = ReadStreamFrameType(headerBytes),
                PacketTotalSize = ReadPacketTotalSize(headerBytes)
            };

            switch (header.FrameType)
            {
                case TrpcDataFrameType.TrpcUnaryFrame:
                    header.MessageHeaderSize = ReadMessageHeaderSize(headerBytes);
                    break;
                case TrpcDataFrameType.TrpcStreamFrame:
                    header.StreamId = ReadStreamId(headerBytes);
                    break;
                default:
                    throw new InvalidDataException($"Not supported tRPC frame type:{(byte)header.FrameType}");
            }
            return true;
        }

        private static ushort ReadTrpcMagic(ReadOnlySpan<byte> headerByte)
        {
            var magic = BinaryPrimitives.ReadUInt16BigEndian(headerByte);
            if (magic != (ushort) TrpcMagic.Value)
            {
                throw new InvalidDataException("No tRPC message detected");
            }

            return magic;
        }

        private static TrpcDataFrameType ReadFrameType(ReadOnlySpan<byte> headerBytes)
        {
            var flag = headerBytes.Slice(PacketHeaderPositions.FrameType_Start)[0];
            return flag switch
            {
                (byte) TrpcDataFrameType.TrpcUnaryFrame => TrpcDataFrameType.TrpcUnaryFrame,
                (byte) TrpcDataFrameType.TrpcStreamFrame => TrpcDataFrameType.TrpcStreamFrame,
                _ => throw new InvalidDataException("Unexpected frame type flag value in message header.")
            };
        }

        private static TrpcStreamFrameType ReadStreamFrameType(ReadOnlySpan<byte> headerBytes)
        {
            var flag = headerBytes.Slice(PacketHeaderPositions.StreamFrameType_Start)[0];
            return flag switch
            {
                (byte) TrpcStreamFrameType.TrpcUnary => TrpcStreamFrameType.TrpcUnary,
                (byte) TrpcStreamFrameType.TrpcStreamFrameData => TrpcStreamFrameType.TrpcStreamFrameData,
                (byte) TrpcStreamFrameType.TrpcStreamFrameFeedback => TrpcStreamFrameType.TrpcStreamFrameFeedback,
                (byte) TrpcStreamFrameType.TrpcStreamFrameClose => TrpcStreamFrameType.TrpcStreamFrameClose,
                (byte) TrpcStreamFrameType.TrpcStreamFrameInit => TrpcStreamFrameType.TrpcStreamFrameInit,
                
                _ => throw new InvalidDataException("Unexpected stream frame type flag value in message header.")
            };
        }

        private static ushort ReadMessageHeaderSize(ReadOnlySpan<byte> frameHeaderBytes)
        {
            var bytes = frameHeaderBytes.Slice(PacketHeaderPositions.MessageHeaderSize_Start);
            return BinaryPrimitives.ReadUInt16BigEndian(bytes);
        }

        private static uint ReadPacketTotalSize(ReadOnlySpan<byte> headerBytes)
        {
            var bytes = headerBytes.Slice(PacketHeaderPositions.PacketSize_Start);
            return BinaryPrimitives.ReadUInt32BigEndian(bytes);
        }

        private static uint ReadStreamId(ReadOnlySpan<byte> headerBytes)
        {
            var bytes = headerBytes.Slice(PacketHeaderPositions.StreamId_Start);
            return BinaryPrimitives.ReadUInt32BigEndian(bytes);
        }


        public static byte[] EncodePacketHeader(PacketHeader header)
        {
            Span<byte> headerBytes = stackalloc byte[PacketHeaderPositions.FrameHeader_TotalLength];
            
            BinaryPrimitives.WriteUInt16BigEndian(headerBytes.Slice(PacketHeaderPositions.Magic_Start), (ushort)TrpcMagic.Value);
            headerBytes[PacketHeaderPositions.FrameType_Start] = (byte) header.FrameType;
            headerBytes[PacketHeaderPositions.StreamFrameType_Start] = (byte) header.StreamFrameType;
            BinaryPrimitives.WriteUInt32BigEndian(headerBytes.Slice(PacketHeaderPositions.PacketSize_Start), header.PacketTotalSize);
            BinaryPrimitives.WriteUInt16BigEndian(headerBytes.Slice(PacketHeaderPositions.MessageHeaderSize_Start), header.MessageHeaderSize);
            BinaryPrimitives.WriteUInt32BigEndian(headerBytes.Slice(PacketHeaderPositions.StreamId_Start), header.StreamId);
            BinaryPrimitives.WriteUInt16BigEndian(headerBytes.Slice(PacketHeaderPositions.Reserved_Start), 0);

            return headerBytes.ToArray();
        }
    }
}