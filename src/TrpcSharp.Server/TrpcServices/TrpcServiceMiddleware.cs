using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TrpcSharp.Server.TrpcServices
{
    public class TrpcServiceMiddleware : ITrpcMiddleware
    {
        private readonly TrpcServiceRouter _router;
        private readonly ILogger<TrpcServiceMiddleware> _logger;
        public TrpcServiceMiddleware(TrpcServiceRouter router, ILogger<TrpcServiceMiddleware> logger)
        {
            _router = router;
            _logger = logger;
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

            await serviceMethodCaller.CallServiceMethod();
        }
    }
}