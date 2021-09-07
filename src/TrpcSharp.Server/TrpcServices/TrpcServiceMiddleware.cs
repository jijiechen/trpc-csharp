using System;
using System.Threading.Tasks;

namespace TrpcSharp.Server.TrpcServices
{
    public class TrpcServiceMiddleware : ITrpcMiddleware
    {
        public TrpcServiceMiddleware()
        {
            
        }
        
        
        public async Task Invoke(TrpcContext trpcContext, TrpcRequestDelegate next)
        {
            var serviceType = trpcContext.Route();
            var activator = trpcContext.Services.GetService(typeof(ITrpcServiceActivator)) as ITrpcServiceActivator;
            if (activator == null)
            {
                throw new ApplicationException("No 'ITrpcServiceActivator' service has been registered");
            }
            
            var service = activator.Create(trpcContext.Services, serviceType);
            invoker(service);
        }
    }
}