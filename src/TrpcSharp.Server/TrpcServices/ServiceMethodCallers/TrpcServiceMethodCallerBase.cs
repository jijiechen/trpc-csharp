using System.Threading.Tasks;

namespace TrpcSharp.Server.TrpcServices.ServiceMethodCallers
{
    public abstract class TrpcServiceMethodCallerBase 
    {
        protected TrpcContext TrpcContext { get; }
        protected TrpcServiceHandle ServiceHandle { get; }
        protected  ITrpcServiceActivator ServiceActivator { get; }
        public TrpcServiceMethodCallerBase(ITrpcServiceActivator serviceActivator, TrpcContext trpcContext, TrpcServiceHandle serviceHandle)
        {
            TrpcContext = trpcContext;
            ServiceHandle = serviceHandle;
            ServiceActivator = serviceActivator;
        }

        public abstract Task CallServiceMethod();
    }
}