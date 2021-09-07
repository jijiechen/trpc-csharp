using System;
using System.Threading.Tasks;
using TrpcSharp.Protocol;

namespace TrpcSharp.Server.TrpcServices
{
    public class TrpcServiceMiddleware : ITrpcMiddleware
    {
        private readonly TrpcServiceRouter _router;
        private readonly TrpcServiceInvoker _invoker;
        public TrpcServiceMiddleware(TrpcServiceRouter router, TrpcServiceInvoker invoker)
        {
            _router = router;
            _invoker = invoker;
        }
        
        public async Task Invoke(TrpcContext trpcContext, TrpcRequestDelegate next)
        {
            await PerformInvocation(trpcContext);
            await next(trpcContext);
        }

        private async Task PerformInvocation(TrpcContext trpcContext)
        {
            string serviceName = null, methodName = null;
            if (trpcContext is UnaryTrpcContext unaryCtx)
            {
                serviceName = unaryCtx.Request.Callee;
                methodName = unaryCtx.Request.Func;
            }
            else if (trpcContext is StreamTrpcContext streamCtx)
            {
                var requestMeta = streamCtx.InitMessage.RequestMeta;
                serviceName = requestMeta?.Callee;
                methodName = requestMeta?.Func;
            }

            if (string.IsNullOrWhiteSpace(serviceName) || string.IsNullOrWhiteSpace(methodName))
            {
                return;
            }
            
            var serviceType = _router.Route(serviceName);
            var activator = trpcContext.Services.GetService(typeof(ITrpcServiceActivator)) as ITrpcServiceActivator;
            if (activator == null)
            {
                throw new ApplicationException("No 'ITrpcServiceActivator' service has been registered");
            }

            var service = activator.Create(trpcContext.Services, serviceType);
            try
            {
                await _invoker.Invoke(service, methodName, trpcContext);
            }
            finally
            {
                await activator.ReleaseAsync(service);
            }
        }
    }
}