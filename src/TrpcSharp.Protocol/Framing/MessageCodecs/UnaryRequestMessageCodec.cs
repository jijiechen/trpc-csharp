using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using Google.Protobuf;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing.MessageCodecs
{
    internal static class UnaryRequestMessageCodec
    {
        public static UnaryRequestMessage Decode(PacketHeader packetHeader, ReadOnlySequence<byte> messageHeaderBytes)
        {
            var reqHeader = RequestProtocol.Parser.ParseFrom(messageHeaderBytes);
            return new UnaryRequestMessage
            {
                RequestId = reqHeader.RequestId,
                Func = reqHeader.Func?.ToStringUtf8(),
                CallType = (TrpcCallType)reqHeader.CallType,
                Caller = reqHeader.Caller?.ToStringUtf8(),
                Callee = reqHeader.Callee?.ToStringUtf8(),
                Timeout = reqHeader.Timeout,
                MessageType = (TrpcMessageType)reqHeader.MessageType,
                ContentType = (TrpcContentEncodeType)reqHeader.ContentType,
                ContentEncoding = (TrpcCompressType)reqHeader.ContentEncoding,
                Metadata = reqHeader.TransInfo.ToMetadata()
            };
            // don't put Data here, since it could be very large
        }

        public static async Task EncodeAsync(UnaryRequestMessage reqMessage, Func<PacketHeader, byte[]> frameHeaderEncoder, Stream output)
        {
            var msgHeader = new RequestProtocol
            {
                Version = (uint)TrpcProtoVersion.TrpcProtoV1,
                RequestId = reqMessage.RequestId,
                Func = ByteString.CopyFromUtf8(reqMessage.Func),
                CallType = (uint)reqMessage.CallType,
                Caller = ByteString.CopyFromUtf8(reqMessage.Caller),
                Callee = ByteString.CopyFromUtf8(reqMessage.Callee),
                Timeout = reqMessage.Timeout,
                MessageType = (uint)reqMessage.MessageType,
                ContentType = (uint)reqMessage.ContentType,
                ContentEncoding = (uint)reqMessage.ContentEncoding,
            };
            reqMessage.Metadata?.CopyTo(msgHeader.TransInfo);

            var msgHeaderLength = msgHeader.CalculateSize();
            var packageTotalLength = PacketHeaderPositions.FrameHeader_TotalLength + msgHeaderLength + (reqMessage.Data?.Length ?? 0);
            if(msgHeaderLength > ushort.MaxValue || packageTotalLength > uint.MaxValue)
            {
                throw new InvalidDataException("Message too large");
            }

            var frameHeader = new PacketHeader
            {
                Magic = (ushort) TrpcMagic.Value,
                FrameType = TrpcDataFrameType.TrpcUnaryFrame,
                StreamFrameType = TrpcStreamFrameType.TrpcUnary,
                MessageHeaderSize = (ushort) msgHeaderLength,
                PacketTotalSize = (uint) packageTotalLength,
                StreamId = 0,
            };
            var headerBytes = frameHeaderEncoder(frameHeader);
            
            output.Write(headerBytes);
            msgHeader.WriteTo(output);
            if (reqMessage.Data != null)
            {
                await reqMessage.Data.CopyToAsync(output);
            }
        }
    }
}