using System.Buffers;
using System.Linq;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing.MessageFramers
{
    internal static class UnaryMessageFramer
    {
        public static UnaryRequestMessage DecodeRequestMessage(FrameHeader frameHeader, ReadOnlySequence<byte> messageBytes)
        {
            var headerBytes = messageBytes.Slice(0, frameHeader.MessageHeaderSize);
            var bodyBytes = messageBytes.Slice(frameHeader.MessageHeaderSize, 
                frameHeader.FrameTotalSize - FrameHeaderPositions.FrameHeader_TotalLength - frameHeader.MessageHeaderSize);
            
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
                TransInfo = msgHeader.TransInfo?.ToDictionary(i => i.Key, i=> i.Value.Memory),
                ContentType = (TrpcContentEncodeType)msgHeader.ContentType,
                ContentEncoding = (TrpcCompressType)msgHeader.ContentEncoding,
                Data =  bodyStream
            };
        }
    }
}