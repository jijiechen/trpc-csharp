using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing
{
    internal struct FrameHeader
    {
        public ushort Magic { get; set; }
        public TrpcDataFrameType FrameType { get; set; }
        public TrpcStreamFrameType StreamFrameType { get; set; }
        public ushort MessageHeaderSize { get; set; }
        public uint FrameTotalSize { get; set; }
        public uint StreamId { get; set; }
    }
}