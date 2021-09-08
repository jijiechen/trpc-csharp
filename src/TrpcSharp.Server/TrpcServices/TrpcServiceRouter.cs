using System.Collections.Generic;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using TrpcSharp.Server.TrpcServices.ServiceMethodCallers;

namespace TrpcSharp.Server.TrpcServices
{
    internal class TrpcServiceRouter
    {
        private readonly Dictionary<string, ServiceMethodRegistration> _services = new();
        private readonly ILogger<TrpcServiceRouter> _logger;

        public TrpcServiceRouter(ILogger<TrpcServiceRouter> logger)
        {
            _logger = logger;
        }

       
        public ITrpcServiceMethodCaller Route(TrpcContext trpcContext)
        {
            string funcName = null;
            if (trpcContext is UnaryTrpcContext unaryCtx)
            {
                funcName = unaryCtx.Request.Func;
            }
            else if (trpcContext is StreamTrpcContext streamCtx)
            {
                var requestMeta = streamCtx.InitMessage.RequestMeta;
                funcName = requestMeta?.Func;
            }

            var specifiedMethodName = !string.IsNullOrWhiteSpace(funcName);
            if (!specifiedMethodName)
            {
                _logger.LogDebug(EventIds.ServiceFuncNotFound, 
                    $"No tRPC service or func found '{funcName}'");
                return null;
            }

            if (!_services.TryGetValue(funcName, out var serviceMethod))
            {
                _logger.LogDebug(EventIds.ServiceFuncNotFound, 
                    $"No tRPC service or func found '{funcName}'");
                return null;
            }
            
            return serviceMethod.Caller;
        }

        private void Register(IMethod method, ITrpcServiceMethodCaller caller, List<object> metadata)
        {
            var registration = new ServiceMethodRegistration
            {
                Method = method,
                Metadata = metadata,
                Caller = caller
            };

            _services[registration.Method.FullName] = registration;
        }

        public void AddUnaryMethod<TService, TRequest, TResponse>(Method<TRequest, TResponse> method, List<object> metadata, 
            TrpcUnaryMethod<TService, TRequest, TResponse> methodExecutor)
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            var caller = new UnaryServiceMethodCaller<TService, TRequest, TResponse>(method, methodExecutor);
            Register(method, caller, metadata);
        }

        public void AddClientStreamingMethod<TService, TRequest, TResponse>(Method<TRequest, TResponse> method, List<object> metadata, 
            TrpcClientStreamingMethod<TService, TRequest> methodExecutor) 
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            Register(method, null, metadata);
        }

        public void AddDuplexStreamingMethod<TService, TRequest, TResponse>(Method<TRequest, TResponse> method, List<object> metadata,
            TrpcDuplexStreamingMethod<TService, TRequest> methodExecutor) 
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            Register(method, null, metadata);
        }

        public void AddServerStreamingMethod<TService, TRequest, TResponse>(Method<TRequest, TResponse> method, List<object> metadata, 
            TrpcServerStreamingMethod<TService, TRequest> methodExecutor)
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            Register(method, null, metadata);
        }
    }
}