using System;
using System.Threading.Tasks;

namespace TrpcSharp.Server.TrpcServices.ServiceMethodCallers
{
    internal class StreamingServiceMethodCaller<TService, TRequest, TResponse> : ITrpcServiceMethodCaller
        where TService : class
        where TRequest : class
        where TResponse : class
    {
        private readonly TrpcClientStreamingMethod<TService, TRequest> _methodExecutor;

        public StreamingServiceMethodCaller(TrpcClientStreamingMethod<TService, TRequest> methodExecutor)
        {
            _methodExecutor = methodExecutor;
        }



        public async Task CallServiceMethod(ITrpcServiceActivator serviceActivator, TrpcContext trpcContext)
        {
            throw new NotImplementedException();
        }
    }

}