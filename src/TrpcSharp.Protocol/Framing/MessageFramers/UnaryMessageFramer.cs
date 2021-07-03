using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing.MessageFramers
{
    internal static class UnaryMessageFramer
    {
        public static UnaryRequestMessage DecodeRequestMessage(PacketHeader packetHeader, ReadOnlySequence<byte> messageBytes)
        {
            var headerBytes = messageBytes.Slice(0, packetHeader.MessageHeaderSize);
            var bodyBytes = messageBytes.Slice(packetHeader.MessageHeaderSize, 
                packetHeader.PacketTotalSize - PacketHeaderPositions.FrameHeader_TotalLength - packetHeader.MessageHeaderSize);
            
            var msgHeader = RequestProtocol.Parser.ParseFrom(headerBytes);
            var bodyStream = new ReadOnlySequenceStream(bodyBytes);
            return new UnaryRequestMessage
            {
                RequestId = msgHeader.RequestId,
                Func = msgHeader.Func?.ToStringUtf8(),
                CallType = (TrpcCallType)msgHeader.CallType,
                Caller = msgHeader.Caller?.ToStringUtf8(),
                Callee = msgHeader.Callee?.ToStringUtf8(),
                Timeout = msgHeader.Timeout,
                MessageType = (TrpcMessageType)msgHeader.MessageType,
                ContentType = (TrpcContentEncodeType)msgHeader.ContentType,
                ContentEncoding = (TrpcCompressType)msgHeader.ContentEncoding,
                AdditionalData = msgHeader.TransInfo?
                    .ToDictionary(i => i.Key, 
                        i=> new TrpcAdditionalData(i.Value.Memory)) 
                                 ?? new Dictionary<string, TrpcAdditionalData>(),
                Data =  bodyStream
            };
        }
        
        
        public static void EncodeRequestMessage(UnaryRequestMessage requestMessage, 
            Func<PacketHeader, byte[]> frameHeaderEncoder, IBufferWriter<byte> output)
        {
            var protocolReq = new RequestProtocol()
            {
                Version = (uint)TrpcProtoVersion.TrpcProtoV1,
                RequestId = requestMessage.RequestId,
                Func = ByteString.CopyFromUtf8(requestMessage.Func),
                CallType = (uint)requestMessage.CallType,
                Caller = ByteString.CopyFromUtf8(requestMessage.Caller),
                Callee = ByteString.CopyFromUtf8(requestMessage.Callee),
                Timeout = requestMessage.Timeout,
                MessageType = (uint)requestMessage.MessageType,
                ContentType = (uint)requestMessage.ContentType,
                ContentEncoding = (uint)requestMessage.ContentEncoding,
            };

            if (requestMessage.AdditionalData != null)
            {
                foreach (var key in requestMessage.AdditionalData.Keys)
                {
                    var item = requestMessage.AdditionalData[key].AsBytes();
                    protocolReq.TransInfo[key] = ByteString.CopyFrom(item.Span);
                }
            }

            var msgHeaderBytes = protocolReq.ToByteArray();
            var packageTotalLength = PacketHeaderPositions.FrameHeader_TotalLength + msgHeaderBytes.Length + (requestMessage.Data?.Length ?? 0);
            if(packageTotalLength > uint.MaxValue)
            {
                throw new InvalidDataException("Message too large");
            }

            var frameHeader = new PacketHeader
            {
                Magic = (ushort) TrpcMagic.Value,
                FrameType = TrpcDataFrameType.TrpcUnaryFrame,
                StreamFrameType = TrpcStreamFrameType.TrpcUnary,
                MessageHeaderSize = (ushort) msgHeaderBytes.Length,
                PacketTotalSize = (uint) packageTotalLength,
                StreamId = 0,
            };
            var headerBytes = frameHeaderEncoder(frameHeader);
            
            output.Write(headerBytes);
            output.Write(msgHeaderBytes.AsSpan());
            if (requestMessage.Data == null)
            {
                return;
            }
            
            const int bufferSize = 4096;
            while (true)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                var bytesRead = requestMessage.Data.Read(buffer, 0, bufferSize);
                if (bytesRead == 0)
                {
                    break;
                }

                output.Write(buffer.AsSpan(0, bytesRead));
                ArrayPool<byte>.Shared.Return(buffer);
                if (bytesRead < bufferSize)
                {
                    break;
                }
            }
        }
    }
}