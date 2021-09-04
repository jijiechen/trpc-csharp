using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using Google.Protobuf;
using TrpcSharp.Protocol.IO;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing.MessageCodecs
{
    internal static class UnaryResponseMessageCodec
    {
        public static UnaryResponseMessage Decode(PacketHeader packetHeader, ReadOnlySequence<byte> messageHeaderBytes)
        {
            var respHeader = ResponseProtocol.Parser.ParseFrom(messageHeaderBytes);
            return new UnaryResponseMessage
            {
                RequestId = respHeader.RequestId,
                ReturnCode = (TrpcRetCode)respHeader.Ret,
                FuncCode = respHeader.FuncRet,
                CallType = (TrpcCallType)respHeader.CallType,
                ErrorMessage =  respHeader.ErrorMsg?.ToStringUtf8(),
                AdditionalData = respHeader.TransInfo.ToAdditionalData(),
                MessageType = (TrpcMessageType)respHeader.MessageType,
                ContentType = (TrpcContentEncodeType)respHeader.ContentType,
                ContentEncoding = (TrpcCompressType)respHeader.ContentEncoding
            };
            // don't put Data here, since it could be very large
        }

        public static async Task EncodeAsync(UnaryResponseMessage respMessage, Func<PacketHeader, byte[]> frameHeaderEncoder, Stream output)
        {
            var msgHeader = new ResponseProtocol
            {
                Version = (uint)TrpcProtoVersion.TrpcProtoV1,
                RequestId = respMessage.RequestId,
                CallType = (uint)respMessage.CallType,
                Ret = (int)respMessage.ReturnCode,
                FuncRet = respMessage.FuncCode,
                ErrorMsg = respMessage.ErrorMessage.ToByteString(),
                MessageType = (uint)respMessage.MessageType,
                ContentType = (uint)respMessage.ContentType,
                ContentEncoding = (uint)respMessage.ContentEncoding,
            };
            respMessage.AdditionalData?.CopyTo(msgHeader.TransInfo);

            var msgHeaderLength = msgHeader.CalculateSize();
            var packageTotalLength = PacketHeaderPositions.FrameHeader_TotalLength + msgHeaderLength + (respMessage.Data?.Length ?? 0);
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
            if (respMessage.Data != null)
            {
               await respMessage.Data.CopyToAsync(output);
            }
        }
    }
}