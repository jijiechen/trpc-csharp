using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace TrpcSharp.Protocol.Framing
{
    public interface ITrpcPacketFramer
    {
        /// <summary>
        /// 尝试以客户端的身份读入消息（可能是 UnaryResponse 或 Stream）
        /// </summary>
        /// <returns>是否已成功读取消息</returns>
        bool TryReadMessageAsClient(ref ReadOnlySequence<byte> buffer, out ITrpcMessage trpcMessage,
            out SequencePosition consumed, out SequencePosition examined);
        
        /// <summary>
        /// 尝试以服务器端的身份读入消息（可能是 UnaryRequest 或 Stream）
        /// </summary>
        /// <returns>是否已成功读取消息</returns>
        bool TryReadMessageAsServer(ref ReadOnlySequence<byte> buffer, out ITrpcMessage trpcMessage,
            out SequencePosition consumed, out SequencePosition examined);

        /// <summary>
        /// 向指定的输出流中写入 tRPC 消息
        /// </summary>
        /// <param name="trpcMessage">要写入的消息</param>
        /// <param name="output">用于输出的目标流</param>
        /// <returns>已写入的字节数</returns>
        Task WriteMessageAsync(ITrpcMessage trpcMessage, Stream output);
    }
}