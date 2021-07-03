using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol.Framing
{
    internal struct PacketHeader
    {
        /// <summary>
        /// tRPC 魔数
        /// </summary>
        public ushort Magic { get; set; }
        /// <summary>
        /// tRPC 包的类型（应答式，或是流式）
        /// </summary>
        public TrpcDataFrameType FrameType { get; set; }
        /// <summary>
        /// tRPC 流式包中的流帧类型（初始化、反馈、数据或关闭）
        /// </summary>
        public TrpcStreamFrameType StreamFrameType { get; set; }
        /// <summary>
        /// 消息的头部大小
        /// </summary>
        public ushort MessageHeaderSize { get; set; }
        /// <summary>
        /// 本次网络包的总大小（包括包头、消息头和消息正文）
        /// </summary>
        public uint PacketTotalSize { get; set; }
        /// <summary>
        /// 流式包的流 ID
        /// </summary>
        public uint StreamId { get; set; }
    }
}