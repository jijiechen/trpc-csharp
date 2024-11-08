﻿using System.Collections.Generic;
using System.IO;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol
{
    public abstract class StreamMessage : ITrpcMessage
    {
        /// <summary>
        /// 流式帧类型
        /// </summary>
        public TrpcStreamFrameType StreamFrameType { get; protected set; }

        /// <summary>
        /// 消息所属的流 Id
        /// </summary>
        public uint StreamId { get; set; }
        
        
        void ITrpcMessage.SetMessageData(Stream stream)
        {
            SetData(stream);
        }

        protected abstract void SetData(Stream stream);
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
        public TrpcMessageType MessageType { get; set; } = TrpcMessageType.TrpcDefault;

        ///<summary>
        /// 附加数据
        ///</summary>
        /// <remarks>
        /// tRPC: trans_info
        /// </remarks>
        public Dictionary<string, TrpcMetadataData> Metadata { get; set; } =
            new Dictionary<string, TrpcMetadataData>();
    }

    public class StreamInitResponseMeta
    {
        /// <summary>
        /// 请求在框架层的错误返回码
        /// </summary>
        /// <remarks>
        /// tRPC: Ret
        /// </remarks>
        public TrpcRetCode ReturnCode { get; set; } = TrpcRetCode.TrpcServerNoserviceErr;
        /// <summary>
        /// 调用结果信息描述
        /// 失败的时候用
        /// </summary>
        /// <remarks>
        /// tRPC: ErrorMsg
        /// </remarks>
        public string ErrorMessage  { get; set; }
    }
    
    public class StreamInitMessage : StreamMessage
    {
        /// <summary>
        /// 默认窗口大小 字节, 2^21 - 1
        /// </summary>
        public const uint DefaultWindowSize  = 2097151;
            
        public StreamInitMessage()
        {
            StreamFrameType = TrpcStreamFrameType.TrpcStreamFrameInit;
        }
        
        /// <summary>
        /// trpc 流式 init 调用中的请求信息
        /// </summary>
        public StreamInitRequestMeta RequestMeta { get; set; }

        /// <summary>
        /// trpc 流式 init 调用中的响应信息
        /// </summary>
        public StreamInitResponseMeta ResponseMeta { get; set; }

        /// <summary>
        /// 发送窗口大小
        /// </summary>
        public uint InitWindowSize { get; set; } = DefaultWindowSize;

        ///<summary>
        /// 请求数据的序列化类型
        ///</summary>
        public TrpcContentEncodeType ContentType { get; set; } = TrpcContentEncodeType.TrpcProtoEncode;

        ///<summary>
        /// 请求数据使用的压缩方式
        ///</summary>
        public TrpcCompressType ContentEncoding { get; set; } = TrpcCompressType.TrpcDefaultCompress;

        
        protected override void SetData(Stream stream)
        {
            throw new System.NotSupportedException();
        }
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
        public Stream Data { get; set; } = Stream.Null;

        protected override void SetData(Stream stream)
        {
            Data = stream;
        }
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

        protected override void SetData(Stream stream)
        {
            throw new System.NotSupportedException();
        }
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
        public TrpcStreamCloseType CloseType { get; set; } = TrpcStreamCloseType.TrpcStreamReset;
        
        /// <summary>
        /// close 返回码
        /// </summary>
        /// <remarks>
        /// tRPC: Ret
        /// </remarks>
        public TrpcRetCode ReturnCode { get; set; } = TrpcRetCode.TrpcServerNoserviceErr;

        /// <summary>
        /// close信息描述
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 业务接口的返回码
        /// </summary>
        /// <remarks>
        /// tRPC: FuncRet
        /// </remarks>
        public int FuncCode { get; set; }

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
        public Dictionary<string, TrpcMetadataData> Metadata { get; set; } =
            new Dictionary<string, TrpcMetadataData>();

        protected override void SetData(Stream stream)
        {
            throw new System.NotSupportedException();
        }
    }
}