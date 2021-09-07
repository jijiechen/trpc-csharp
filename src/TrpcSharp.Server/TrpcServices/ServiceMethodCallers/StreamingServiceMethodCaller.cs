using System;
using System.Threading.Tasks;

namespace TrpcSharp.Server.TrpcServices.ServiceMethodCallers
{
    internal class StreamingServiceMethodCaller<TService, TRequest, TResponse> : TrpcServiceMethodCallerBase
        where TService : class
        where TRequest : class
        where TResponse : class
    {
        private readonly TrpcUnaryMethod<TService, TRequest, TResponse> _methodInvoker;

        public StreamingServiceMethodCaller(ITrpcServiceActivator serviceActivator, TrpcContext trpcContext, 
            TrpcServiceHandle serviceHandle, TrpcUnaryMethod<TService, TRequest, TResponse> methodInvoker) : base(serviceActivator, trpcContext, serviceHandle)
        {
            _methodInvoker = methodInvoker;
        }

        public override Task CallServiceMethod()
        {
            if (ServiceHandle.Instance == null)
            {
                throw new ApplicationException("Service instance has not been initialized");
            }

            Task<TResponse> invokerTask = null;
            try
            {

            }
            catch (Exception ex)
            {

                return Task.FromException<TResponse>(ex);
            }

            return Task.CompletedTask;
        }
    }

}