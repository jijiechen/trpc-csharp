using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing
{
    public struct FrameHeader
    {
        public ushort Magic { get; set; }
        public TrpcDataFrameType FrameType { get; set; }
        public TrpcStreamFrameType StreamFrameType { get; set; }
        public ushort MessageHeaderSize { get; set; }
        public long FrameTotalSize { get; set; }
        public long StreamId { get; set; }
    }
}