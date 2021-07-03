using System;
using System.Collections.Generic;
using System.IO;
using TrpcSharp.Protocol.Framing;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol
{
    public abstract class StreamMessage : ITrpcRequestMessage
    {
        /// <summary>
        /// 流式帧类型
        /// </summary>
        public TrpcStreamFrameType StreamFrameType { get; protected set; }

        /// <summary>
        /// 消息所属的流 Id
        /// </summary>
        public uint StreamId { get; set; }
    }
    
    
    public class StreamInitRequestMeta
    {
        ///<summary>
        /// 请求的函数名
        ///</summary>
        public string Func { get; set; }

        ///<summary>
        /// 主调服务的路由名称
        ///</summary>
        public string Caller { get; set; }

        ///<summary>
        /// 被调服务的路由名称
        ///</summary>
        public string Callee { get; set; }

        ///<summary>
        /// 框架信息透传的消息类型
        ///</summary>
        public TrpcMessageType MessageType { get; set; }

        ///<summary>
        /// 附加数据 trans_info
        ///</summary>
        public Dictionary<string, TrpcAdditionalData> AdditionalData { get; set; }
    }

    public class StreamInitResponseMeta
    {
        public TrpcRetCode ReturnCode { get; set; }
        public string ErrorMessage  { get; set; }
    }
    
    public class StreamInitMessage : StreamMessage
    {
        public StreamInitMessage()
        {
            StreamFrameType = TrpcStreamFrameType.TrpcStreamFrameInit;
        }
        
        /// <summary>
        /// trpc流式init头的请求元信息
        /// </summary>
        public StreamInitRequestMeta RequestMeta { get; set; }

        /// <summary>
        /// trpc流式init头的响应元信息
        /// </summary>
        public StreamInitResponseMeta ResponseMeta { get; set; }

        /// <summary>
        /// 发送窗口大小
        /// </summary>
        public uint InitWindowSize { get; set; }

        ///<summary>
        /// 请求数据的序列化类型
        ///</summary>
        public TrpcContentEncodeType ContentType { get; set; }

        ///<summary>
        /// 请求数据使用的压缩方式
        ///</summary>
        public TrpcCompressType ContentEncoding { get; set; }
    }

    public class StreamDataMessage : StreamMessage
    {
        public StreamDataMessage()
        {
            StreamFrameType = TrpcStreamFrameType.TrpcStreamFrameData;
        }
        
        ///<summary>
        /// 从流中收到的数据
        ///</summary>
        public Stream Data { get; set; }
    }

    public class StreamFeedbackMessage : StreamMessage
    {
        public StreamFeedbackMessage()
        {
            StreamFrameType = TrpcStreamFrameType.TrpcStreamFrameFeedback;
        }
        
        /// <summary>
        /// 增加的窗口大小
        /// </summary>
        public uint WindowSizeIncrement { get; set; }
    }

    public class StreamCloseMessage : StreamMessage
    {
        public StreamCloseMessage()
        {
            StreamFrameType = TrpcStreamFrameType.TrpcStreamFrameClose;
        }
        
        /// <summary>
        /// 关闭的类型，关闭一端，还是全部关闭
        /// </summary>
        public TrpcStreamCloseType CloseType { get; set; }

        /// <summary>
        /// close 返回码
        /// </summary>
        public int ReturnCode { get; set; }

        /// <summary>
        /// close信息描述
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 业务接口的返回码
        /// </summary>
        public int FuncCode { get; set; }

        public TrpcMessageType MessageType { get; set; }
        public Dictionary<string, TrpcAdditionalData> AdditionalData { get; set; }
    }
}