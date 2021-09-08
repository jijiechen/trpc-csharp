using System;
using Grpc.Core;

namespace TrpcSharp.Server.TrpcServices
{
    public abstract class TrpcServiceBinderBase
    {
        public virtual void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method,
            TrpcUnaryMethod<TRequest, TResponse> handler)
            where TRequest : class
            where TResponse : class
        {
            throw new NotImplementedException();
        }

        public virtual void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, TrpcClientStreamingMethod<TRequest> handler) 
            where TRequest : class
            where TResponse : class
        {
            throw new NotImplementedException();
        }

        public virtual void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, TrpcDuplexStreamingMethod<TRequest> handler) 
            where TRequest : class
            where TResponse : class
        {
            throw new NotImplementedException();
        }

        public virtual void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, TrpcServerStreamingMethod<TRequest> handler)
            where TRequest : class
            where TResponse : class
        {
            throw new NotImplementedException();
        }
    }
    
    
}