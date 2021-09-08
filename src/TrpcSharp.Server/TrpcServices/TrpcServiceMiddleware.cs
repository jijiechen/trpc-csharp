using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TrpcSharp.Server.TrpcServices
{
    internal class TrpcServiceMiddleware : ITrpcMiddleware
    {
        private readonly TrpcServiceRouter _router;
        private readonly ILogger<TrpcServiceMiddleware> _logger;
        private readonly ITrpcServiceActivator _serviceActivator;
        public TrpcServiceMiddleware(TrpcServiceRouter router, ITrpcServiceActivator serviceActivator, ILogger<TrpcServiceMiddleware> logger)
        {
            _router = router;
            _logger = logger;
            _serviceActivator = serviceActivator;
        }
        
        public async Task Invoke(TrpcContext trpcContext, TrpcRequestDelegate next)
        {
            await PerformInvocation(trpcContext);
            await next(trpcContext);
        }

        private async Task PerformInvocation(TrpcContext trpcContext)
        {
            var serviceMethodCaller = _router.Route(trpcContext);
            if (serviceMethodCaller == null)
            {
                return;
            }

            await serviceMethodCaller.CallServiceMethod(_serviceActivator, trpcContext);
        }
    }
}