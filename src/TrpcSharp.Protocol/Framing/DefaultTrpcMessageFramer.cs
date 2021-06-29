using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using TrpcSharp.Protocol.Framing.Unary;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing
{
    public class DefaultTrpcMessageFramer : ITrpcMessageFramer
    {
        public bool TryParseMessage(ref ReadOnlySequence<byte> buffer, out UnaryRequestMessage trpcMessage, 
            out SequencePosition consumed, out SequencePosition examined)
        {
            examined = consumed = buffer.Start;
            var hasHeader = TryReadFrameHeader(buffer, out var frameHeader);
            if (hasHeader)
            {
                if (buffer.Length < frameHeader.FrameTotalSize)
                {
                    examined = buffer.End;
                    trpcMessage = null;
                    return false;
                }
                
                var messageBytes = buffer.Slice(FrameHeaderPositions.FrameHeader_TotalLength); 
                if (frameHeader.FrameType == TrpcDataFrameType.TrpcUnaryFrame)
                {
                    trpcMessage = UnaryFramer.DecodeRequestMessage(frameHeader, messageBytes);
                    examined = consumed = buffer.GetPosition(frameHeader.FrameTotalSize, buffer.Start);
                    return true;
                }
            }
            else
            {
                examined = buffer.End;
            }
            
            trpcMessage = null;
            return false;
        }
        
        private static bool TryReadFrameHeader(in ReadOnlySequence<byte> buffer, out FrameHeader header)
        {
            if (buffer.Length < FrameHeaderPositions.FrameHeader_TotalLength)
            {   
                header = new FrameHeader();
                return false;
            }

            if (buffer.First.Length >= FrameHeaderPositions.FrameHeader_TotalLength)
            {
                var headerSpan = buffer.First.Span.Slice(0, FrameHeaderPositions.FrameHeader_TotalLength);
                return ReadFrameHeader(headerSpan, out header);
            }
            else
            {
                Span<byte> headerBytes = stackalloc byte[FrameHeaderPositions.FrameHeader_TotalLength];
                
                buffer.Slice(0, FrameHeaderPositions.FrameHeader_TotalLength).CopyTo(headerBytes);
                return ReadFrameHeader(headerBytes, out header);
            }
        }
        
        private static bool ReadFrameHeader(in ReadOnlySpan<byte> headerBytes, out FrameHeader header)
        {
            header = new FrameHeader
            {
                Magic = ReadTrpcMagic(headerBytes),
                FrameType = ReadFrameType(headerBytes),
                StreamFrameType = ReadStreamFrameType(headerBytes),
                FrameTotalSize = ReadMessageBodySize(headerBytes),
                StreamId = ReadStreamId(headerBytes)
            };

            if (header.FrameType == TrpcDataFrameType.TrpcUnaryFrame)
            {
                header.MessageHeaderSize = ReadMessageHeaderSize(headerBytes);
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
            var flag = headerBytes.Slice(FrameHeaderPositions.FrameType_Start)[0];
            return flag switch
            {
                (byte) TrpcDataFrameType.TrpcUnaryFrame => TrpcDataFrameType.TrpcUnaryFrame,
                (byte) TrpcDataFrameType.TrpcStreamFrame => TrpcDataFrameType.TrpcStreamFrame,
                _ => throw new InvalidDataException("Unexpected frame type flag value in message header.")
            };
        }

        private static TrpcStreamFrameType ReadStreamFrameType(ReadOnlySpan<byte> headerBytes)
        {
            var flag = headerBytes.Slice(FrameHeaderPositions.StreamFrameType_Start)[0];
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
            var bytes = frameHeaderBytes.Slice(FrameHeaderPositions.HeadSize_Start);
            return BinaryPrimitives.ReadUInt16BigEndian(bytes);
        }

        private static long ReadMessageBodySize(ReadOnlySpan<byte> headerBytes)
        {
            var bytes = headerBytes.Slice(FrameHeaderPositions.PacketSize_Start);
            return BinaryPrimitives.ReadUInt32BigEndian(bytes);
        }

        private static long ReadStreamId(ReadOnlySpan<byte> headerBytes)
        {
            var bytes = headerBytes.Slice(FrameHeaderPositions.StreamId_Start);
            return BinaryPrimitives.ReadUInt32BigEndian(bytes);
        }
    }
}