using System;
using System.Collections.Generic;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using TrpcSharp.Server.TrpcServices.ServiceMethodCallers;

namespace TrpcSharp.Server.TrpcServices
{
    public class TrpcServiceRouter
    {
        private readonly Dictionary<string, Type> _services = new();
        private readonly ITrpcServiceActivator _serviceActivator;
        private readonly ILogger<TrpcServiceRouter> _logger;

        public TrpcServiceRouter(ITrpcServiceActivator serviceActivator, ILogger<TrpcServiceRouter> logger)
        {
            _serviceActivator = serviceActivator;
            _logger = logger;
        }

        public void Register(string service, Type serviceType)
        {
            if (string.IsNullOrEmpty(service))
            {
                throw new ArgumentException("A service must have a name");
            }
            
            _services[service] = serviceType;
        }
        
        public TrpcServiceMethodCallerBase Route(TrpcContext trpcContext)
        {
            string serviceName = null, funcName = null;
            if (trpcContext is UnaryTrpcContext unaryCtx)
            {
                serviceName = unaryCtx.Request.Callee;
                funcName = unaryCtx.Request.Func;
            }
            else if (trpcContext is StreamTrpcContext streamCtx)
            {
                var requestMeta = streamCtx.InitMessage.RequestMeta;
                serviceName = requestMeta?.Callee;
                funcName = requestMeta?.Func;
            }

            var specifiedServiceName = string.IsNullOrWhiteSpace(serviceName);
            var specifiedMethodName = string.IsNullOrWhiteSpace(funcName);
            if (!specifiedServiceName || !specifiedMethodName)
            {
                _logger.LogDebug(EventIds.ServiceFuncNotFound, 
                    $"No tRPC service or func found '{serviceName}/{funcName}'");
                return null;
            }

            Type serviceType;
            Type methodInvoker;
            var serviceHandle = _serviceActivator.Create(trpcContext.Services, serviceType);
            return new UnaryServiceMethodCaller<,,>(_serviceActivator, trpcContext, serviceHandle, methodInvoker);
        }

        public void AddUnaryMethod<TService, TRequest, TResponse>(Method<TRequest, TResponse> method, List<object> metadata, 
            TrpcUnaryMethod<TService, TRequest, TResponse> methodInvoker)
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            throw new NotImplementedException();
        }

        public void AddClientStreamingMethod<TService, TRequest, TResponse>(Method<TRequest, TResponse> method, List<object> metadata, 
            TrpcClientStreamingMethod<TService, TRequest> methodInvoker) 
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            throw new NotImplementedException();
        }

        public void AddDuplexStreamingMethod<TService, TRequest, TResponse>(Method<TRequest, TResponse> method, List<object> metadata,
            TrpcDuplexStreamingMethod<TService, TRequest> methodInvoker) 
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            throw new NotImplementedException();
        }

        public void AddServerStreamingMethod<TService, TRequest, TResponse>(Method<TRequest, TResponse> method, List<object> metadata, 
            TrpcServerStreamingMethod<TService, TRequest> methodInvoker)
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            throw new NotImplementedException();
        }
    }
}