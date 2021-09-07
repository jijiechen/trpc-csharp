using System.Reflection;
using System.Threading.Tasks;

namespace TrpcSharp.Server.TrpcServices
{
    public class TrpcServiceInvoker
    {
        public async Task Invoke(TrpcActivatorHandle service, string methodName, TrpcContext trpcContext)
        {
            var type = service.GetType();
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
            {
                // complete and go
                // todo: not found
            }
            
            // todo: if async, invoke async
            // todo: check parameters, pass in and invoke it!
           
        }
    }
    
}