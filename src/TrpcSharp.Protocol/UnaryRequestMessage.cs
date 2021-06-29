using System;
using System.Collections.Generic;
using System.IO;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Protocol
{
    public class UnaryRequestMessage
    {
        ///<summary>
        /// 框架生成的请求序列号
        ///</summary>
        public uint RequestId{get;set;} 

        ///<summary>
        /// 请求的函数名
        ///</summary>
        public string FunctionName {get;set;} 

        ///<summary>
        /// 调用类型
        ///</summary>
        public TrpcCallType CallType {get;set;} 

        ///<summary>
        /// 主调服务的路由名称
        ///</summary>
        public string Caller{get;set;} 

        ///<summary>
        /// 被调服务的路由名称
        ///</summary>
        public string Callee{get;set;} 

        ///<summary>
        /// 客户端超时时间
        ///</summary>
        public uint Timeout{get;set;} 

        ///<summary>
        /// 框架信息透传的消息类型
        ///</summary>
        public TrpcMessageType MessageType{get;set;} 

        ///<summary>
        /// 附加数据 trans_info
        ///</summary>
        public  Dictionary<string, ReadOnlyMemory<byte>> Context {get;set;} 

        ///<summary>
        /// 请求数据的序列化类型
        ///</summary>
        public TrpcContentEncodeType ContentType{get;set;} 

        ///<summary>
        /// 请求数据使用的压缩方式
        ///</summary>
        public TrpcCompressType ContentEncoding {get;set;} 

        ///<summary>
        /// 从请求中收到的数据
        ///</summary>
        public Stream Data {get;set;} 
    }

    public class UnaryResponseMessage{
        
        ///<summary>
        /// 框架生成的请求序列号
        ///</summary>
        public int RequestId{get;set;} 

        ///<summary>
        /// 框架的返回码
        ///</summary>
        public TrpcRetCode RetCode{get;set;} 

        ///<summary>
        /// 业务接口的返回码
        ///</summary>
        public int FuncCode {get;set;} 

        ///<summary>
        /// 调用类型
        ///</summary> 
        public TrpcCallType CallType {get;set;} 

        ///<summary>
        /// 错误信息
        ///</summary>
        public string ErrMessage {get;set;} 

        ///<summary>
        /// 附加数据 trans_info
        ///</summary>
        // public Record<string, Uint8Array> Context {get;set;} 

        ///<summary>
        /// 框架信息透传的消息类型
        ///</summary>
        public TrpcMessageType MessageType {get;set;} 

        ///<summary>
        /// 请求数据的序列化类型
        ///</summary>
        public TrpcContentEncodeType ContentType {get;set;} 

        ///<summary>
        /// 请求数据使用的压缩方式
        ///</summary>
        public TrpcCompressType ContentEncoding {get;set;} 

        ///<summary>
        /// 请求发送的数据
        ///</summary>
        public Stream Data {get;set;}
    }
}