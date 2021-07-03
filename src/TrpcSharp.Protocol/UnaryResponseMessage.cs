using System.Collections.Generic;
using System.IO;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol
{
    public class UnaryResponseMessage : ITrpcMessage
    {
        ///<summary>
        /// 框架生成的请求序列号
        ///</summary>
        public uint RequestId { get; set; }
        /// <summary>
        /// 请求在框架层的错误返回码
        /// </summary>
        /// <remarks>
        /// tRPC: Ret
        /// </remarks>
        public TrpcRetCode ReturnCode { get; set; }
        /// <summary>
        /// 业务接口的返回码
        /// </summary>
        /// <remarks>
        /// tRPC: FuncRet
        /// </remarks>
        public int FuncCode { get; set; }

        ///<summary>
        /// 调用类型
        ///</summary>
        public TrpcCallType CallType { get; set; } = TrpcCallType.TrpcUnaryCall;

        /// <summary>
        /// 调用结果信息描述
        /// 失败的时候用
        /// </summary>
        /// <remarks>
        /// tRPC: ErrorMsg
        /// </remarks>
        public string ErrorMessage  { get; set; }

        ///<summary>
        /// 附加数据
        ///</summary>
        /// <remarks>
        /// tRPC: trans_info
        /// </remarks>
        public IReadOnlyDictionary<string, TrpcAdditionalData> AdditionalData { get; set; }

        ///<summary>
        /// 框架信息透传的消息类型
        ///</summary>
        public TrpcMessageType MessageType { get; set; } = TrpcMessageType.TrpcDefault;

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
        public Stream Data { get; set; }
    }
}