using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol
{
    public class UnaryRequestMessage : ITrpcMessage
    {
        ///<summary>
        /// 框架生成的请求序列号
        ///</summary>
        public uint RequestId { get; set; }

        ///<summary>
        /// 请求的函数名
        ///</summary>
        public string Func { get; set; }

        ///<summary>
        /// 调用类型
        ///</summary>
        public TrpcCallType CallType { get; set; } = TrpcCallType.TrpcUnaryCall;

        ///<summary>
        /// 主调服务的路由名称
        ///</summary>
        public string Caller { get; set; }

        ///<summary>
        /// 被调服务的路由名称
        ///</summary>
        public string Callee { get; set; }

        ///<summary>
        /// 客户端超时时间
        ///</summary>
        public uint Timeout { get; set; }

        ///<summary>
        /// 框架信息透传的消息类型
        ///</summary>
        public TrpcMessageType MessageType { get; set; } = TrpcMessageType.TrpcDefault;

        ///<summary>
        /// 附加数据
        ///</summary>
        /// <remarks>
        /// tRPC: trans_info
        /// </remarks>
        public IReadOnlyDictionary<string, TrpcAdditionalData> AdditionalData { get; set; }  =
            new Dictionary<string, TrpcAdditionalData>();

        ///<summary>
        /// 请求数据的序列化类型
        ///</summary>
        public TrpcContentEncodeType ContentType { get; set; }

        ///<summary>
        /// 请求数据使用的压缩方式
        ///</summary>
        public TrpcCompressType ContentEncoding { get; set; } = TrpcCompressType.TrpcDefaultCompress;

        ///<summary>
        /// 从请求中收到的数据
        ///</summary>
        public Stream Data { get; set; } = Stream.Null;

        void ITrpcMessage.SetMessageData(Stream stream)
        {
            Data = stream;
        }
    }

    public sealed class TrpcAdditionalData
    {
        private readonly ReadOnlyMemory<byte> _mem;

        public TrpcAdditionalData(string strValue)
        {
            if (strValue == null)
            {
                throw new ArgumentNullException(nameof(strValue));
            }

            _mem = Encoding.UTF8.GetBytes(strValue);
        }

        public TrpcAdditionalData(byte[] bytes) : this((ReadOnlyMemory<byte>) bytes)
        {
        }

        public TrpcAdditionalData(ReadOnlyMemory<byte> bytes)
        {
            _mem = bytes;
        }


        public string AsString() => Encoding.UTF8.GetString(_mem.Span);

        public ReadOnlyMemory<byte> AsBytes() => _mem;
    }
}