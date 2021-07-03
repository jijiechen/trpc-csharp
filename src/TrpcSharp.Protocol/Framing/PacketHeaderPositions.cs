// ReSharper disable InconsistentNaming
namespace TrpcSharp.Protocol.Framing
{
    internal static class PacketHeaderPositions
    {
        public const byte FrameHeader_TotalLength = 16;

        public const int Magic_Start = 0;
        public const int Magic_Length = 2;

        public const int FrameType_Start = Magic_Start + Magic_Length;
        public const int FrameType_Length = 1;
        
        public const int StreamFrameType_Start = FrameType_Start + FrameType_Length;
        public const int StreamFrameType_Length = 1;
        
        public const int PacketSize_Start = StreamFrameType_Start + StreamFrameType_Length;
        public const int PacketSize_Length = 4;

        public const int MessageHeaderSize_Start = PacketSize_Start + PacketSize_Length;
        public const int MessageHeaderSize_Length = 2;

        public const int StreamId_Start = MessageHeaderSize_Start + MessageHeaderSize_Length;
        public const int StreamId_Length = 4;

        public const int Reserved_Start = StreamId_Start + StreamId_Length;
        public const int Reserved_Length = 2;
    }
}