using System;
using System.Buffers;
using System.IO;
using System.Linq;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing.MessageFramers
{
    internal static class StreamMessageFramer
    {
        public static StreamRequestMessage DecodeRequestMessage(PacketHeader packetHeader, ReadOnlySequence<byte> messageBytes)
        {
            switch (packetHeader.StreamFrameType)
            {
                case TrpcStreamFrameType.TrpcStreamFrameData:
                    return DecodeDataMessage(packetHeader.StreamId, messageBytes);
                case TrpcStreamFrameType.TrpcStreamFrameInit:
                    return DecodeInitMessage(packetHeader.StreamId, messageBytes);
                case TrpcStreamFrameType.TrpcStreamFrameFeedback:
                    return DecodeFeedbackMessage(packetHeader.StreamId, messageBytes);
                case TrpcStreamFrameType.TrpcStreamFrameClose:
                    return DecodeCloseMessage(packetHeader.StreamId, messageBytes);
                default:
                    throw new InvalidDataException($"Not supported tRPC frame type:{(byte)packetHeader.StreamFrameType}");
            }
        }


        static StreamRequestMessage DecodeDataMessage(uint streamId, ReadOnlySequence<byte> bytes)
        {
            return new StreamDataMessage
            {
                StreamId = streamId,
                Data = new ReadOnlySequenceStream(bytes)
            };
        }

        static StreamRequestMessage DecodeInitMessage(uint streamId, ReadOnlySequence<byte> bytes)
        {
            var meta = TrpcStreamInitMeta.Parser.ParseFrom(bytes);
            var decodedMessage = new StreamInitMessage
            {
                StreamId = streamId,
                InitWindowSize = meta.InitWindowSize,
                ContentType = (TrpcContentEncodeType)meta.ContentType,
                ContentEncoding = (TrpcCompressType)meta.ContentEncoding,
                
            };

            if (meta.RequestMeta != null)
            {
                var requestMeta = new StreamInitRequestMeta()
                {
                    Caller = meta.RequestMeta.Caller?.ToStringUtf8(),
                    Callee = meta.RequestMeta.Callee?.ToStringUtf8(),
                    Func = meta.RequestMeta.Func?.ToStringUtf8(),
                    MessageType = (TrpcMessageType) meta.RequestMeta.MessageType,
                    TransInfo = meta.RequestMeta.TransInfo?.ToDictionary(i => i.Key, i => i.Value.Memory),
                };
                decodedMessage.RequestMeta = requestMeta;
            }
            
            if (meta.ResponseMeta != null)
            {
                var responseMeta = new StreamInitResponseMeta()
                {
                    ReturnCode = (TrpcRetCode)meta.ResponseMeta.Ret,
                    ErrorMessage = meta.ResponseMeta.ErrorMsg?.ToStringUtf8()
                };
                decodedMessage.ResponseMeta = responseMeta;
            }

            return decodedMessage;
        }

        static StreamRequestMessage DecodeFeedbackMessage(uint streamId, ReadOnlySequence<byte> bytes)
        {
            var meta = TrpcStreamFeedBackMeta.Parser.ParseFrom(bytes);
            return new StreamFeedbackMessage{
                StreamId = streamId,
                WindowSizeIncrement = meta.WindowSizeIncrement
            };
        }

        static StreamRequestMessage DecodeCloseMessage(uint streamId, ReadOnlySequence<byte> bytes)
        {
            var meta = TrpcStreamCloseMeta.Parser.ParseFrom(bytes);

            return new StreamCloseMessage
            {
                StreamId = streamId,
                CloseType = (TrpcStreamCloseType) meta.CloseType,
                RetCode = meta.Ret,
                FuncCode = meta.FuncRet,
                Message = meta.Msg?.ToStringUtf8(),
                MessageType = (TrpcMessageType) meta.MessageType,
                TransInfo = meta.TransInfo?.ToDictionary(i => i.Key, i => i.Value.Memory),
            };
        }
    
        public static void EncodeRequestMessage(StreamRequestMessage streamMsg, Func<PacketHeader, byte[]> encodePacketHeader, IBufferWriter<byte> output)
        {
            throw new NotImplementedException();
        }
    }
}