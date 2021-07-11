using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using TrpcSharp.Protocol.IO;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing.MessageCodecs
{
    internal static class StreamMessageCodec
    {
        public static StreamMessage Decode(PacketHeader packetHeader, ReadOnlySequence<byte> messageBytes)
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
                    throw new InvalidDataException($"Unsupported tRPC stream frame type:{(byte)packetHeader.StreamFrameType}");
            }
        }

        static StreamMessage DecodeDataMessage(uint streamId, ReadOnlySequence<byte> bytes)
        {
            return new StreamDataMessage
            {
                StreamId = streamId,
                Data = new ReadOnlySequenceStream(bytes)
            };
        }

        static StreamMessage DecodeInitMessage(uint streamId, ReadOnlySequence<byte> bytes)
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
                    AdditionalData = meta.RequestMeta.TransInfo.ToAdditionalData(),
                };
                decodedMessage.RequestMeta = requestMeta;
            }
            
            if (meta.ResponseMeta != null)
            {
                var responseMeta = new StreamInitResponseMeta()
                {
                    ReturnCode = (TrpcRetCode)meta.ResponseMeta.Ret,
                    ErrorMessage = meta.ResponseMeta.ErrorMsg.ToStringUtf8()
                };
                decodedMessage.ResponseMeta = responseMeta;
            }

            return decodedMessage;
        }

        static StreamMessage DecodeFeedbackMessage(uint streamId, ReadOnlySequence<byte> bytes)
        {
            var meta = TrpcStreamFeedBackMeta.Parser.ParseFrom(bytes);
            return new StreamFeedbackMessage{
                StreamId = streamId,
                WindowSizeIncrement = meta.WindowSizeIncrement
            };
        }

        static StreamMessage DecodeCloseMessage(uint streamId, ReadOnlySequence<byte> bytes)
        {
            var meta = TrpcStreamCloseMeta.Parser.ParseFrom(bytes);

            return new StreamCloseMessage
            {
                StreamId = streamId,
                CloseType = (TrpcStreamCloseType) meta.CloseType,
                ReturnCode = (TrpcRetCode)meta.Ret,
                FuncCode = meta.FuncRet,
                Message = meta.Msg?.ToStringUtf8(),
                MessageType = (TrpcMessageType) meta.MessageType,
                AdditionalData = meta.TransInfo.ToAdditionalData(),
            };
        }
    
        public static async Task EncodeAsync(StreamMessage streamMsg, Func<PacketHeader, byte[]> frameHeaderEncoder, Stream output)
        {
            IMessage metaMessage = null;
            Stream dataMsgBody = null;
            switch (streamMsg)
            {
                case StreamDataMessage dataMsg:
                    dataMsgBody = dataMsg.Data;
                    break;
                case StreamInitMessage initMsg:
                    metaMessage = ComposeTrpcInitMeta(initMsg);
                    break;
                case StreamFeedbackMessage feedbackMsg:
                    metaMessage = new TrpcStreamFeedBackMeta
                    {
                        WindowSizeIncrement = feedbackMsg.WindowSizeIncrement
                    };
                    break;
                case StreamCloseMessage closeMsg:
                    metaMessage = ComposeTrpcCloseMeta(closeMsg);
                    break;
            }

            var streamMetaLength = (metaMessage?.CalculateSize() ?? 0);
            var streamDataLength = (dataMsgBody?.Length ?? 0);
            var totalLength =  PacketHeaderPositions.FrameHeader_TotalLength + streamMetaLength + streamDataLength;
            if (totalLength > uint.MaxValue)
            {
                throw new InvalidDataException("Message too large");
            }
            
            var frameHeader = new PacketHeader
            {
                Magic = (ushort) TrpcMagic.Value,
                FrameType = TrpcDataFrameType.TrpcStreamFrame,
                StreamFrameType = streamMsg.StreamFrameType,
                MessageHeaderSize = 0,
                PacketTotalSize = (uint) totalLength,
                StreamId = streamMsg.StreamId,
            };
            var headerBytes = frameHeaderEncoder(frameHeader);
            
            output.Write(headerBytes);
            metaMessage?.WriteTo(output);
            if (dataMsgBody != null)
            {
              await dataMsgBody.CopyToAsync(output);
            }
        }

        static IMessage ComposeTrpcInitMeta(StreamInitMessage initMsg)
        {
            var meta = new TrpcStreamInitMeta
            {
                ContentType = (uint) initMsg.ContentType,
                ContentEncoding = (uint) initMsg.ContentEncoding,
                InitWindowSize = initMsg.InitWindowSize
            };
            
            if (initMsg.RequestMeta != null)
            {
                meta.RequestMeta = new TrpcStreamInitRequestMeta
                {
                    Caller = initMsg.RequestMeta.Caller.ToByteString(),
                    Callee = initMsg.RequestMeta.Callee.ToByteString(),
                    Func = initMsg.RequestMeta.Func.ToByteString(),
                    MessageType = (uint) initMsg.RequestMeta.MessageType
                };
                initMsg.RequestMeta.AdditionalData?.CopyTo(meta.RequestMeta.TransInfo);
            }
            
            if (initMsg.ResponseMeta != null)
            {
                meta.ResponseMeta = new TrpcStreamInitResponseMeta
                {
                    Ret = (int)initMsg.ResponseMeta.ReturnCode,
                    ErrorMsg =  initMsg.ResponseMeta.ErrorMessage.ToByteString(),
                };
            }
            return meta;
        }

        static TrpcStreamCloseMeta ComposeTrpcCloseMeta(StreamCloseMessage closeMsg)
        {
            var meta = new TrpcStreamCloseMeta
            {
                CloseType = (int) closeMsg.CloseType,
                Ret = (int)closeMsg.ReturnCode,
                FuncRet = closeMsg.FuncCode,
                Msg = closeMsg.Message.ToByteString(),
                MessageType = (uint) closeMsg.MessageType,
            };
            closeMsg.AdditionalData?.CopyTo(meta.TransInfo);
            return meta;
        }
    }
}