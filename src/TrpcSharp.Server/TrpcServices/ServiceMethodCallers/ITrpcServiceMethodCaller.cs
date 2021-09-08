using System.Threading.Tasks;

namespace TrpcSharp.Server.TrpcServices.ServiceMethodCallers
{
    public interface ITrpcServiceMethodCaller 
    {
        Task CallServiceMethod(ITrpcServiceActivator serviceActivator, TrpcContext trpcContext);
    }
}