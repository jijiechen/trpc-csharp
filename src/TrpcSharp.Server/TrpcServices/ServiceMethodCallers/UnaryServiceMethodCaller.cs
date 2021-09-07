using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace TrpcSharp.Server.TrpcServices.ServiceMethodCallers
{
    internal class UnaryServiceMethodCaller<TService, TRequest, TResponse> : TrpcServiceMethodCallerBase
        where TService : class
        where TRequest : class
        where TResponse : class
    {
        private readonly TrpcUnaryMethod<TService, TRequest, TResponse> _methodInvoker;

        public UnaryServiceMethodCaller(ITrpcServiceActivator serviceActivator, TrpcContext trpcContext, 
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
                var unaryContext = TrpcContext as UnaryTrpcContext;

                var request = unaryContext.Request; 
                // var response = unaryContext.Response;
                
                invokerTask = _methodInvoker(
                    ServiceHandle.Instance as TService,
                    request as TRequest,
                    unaryContext);
                
                // unaryContext.Response.Data
            }
            catch (Exception ex)
            {
                // Invoker calls user code. User code may throw an exception instead
                // of a faulted task. We need to catch the exception, ensure cleanup
                // runs and convert exception into a faulted task.
                if (ServiceHandle.Instance != null)
                {
                    var releaseTask = ServiceActivator.ReleaseAsync(ServiceHandle);
                    if (!releaseTask.IsCompletedSuccessfully)
                    {
                        // Capture the current exception state so we can rethrow it after awaiting
                        // with the same stack trace.
                        var exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
                        return AwaitServiceReleaseAndThrow(releaseTask, exceptionDispatchInfo);
                    }
                }

                return Task.FromException<TResponse>(ex);
            }

            if (invokerTask.IsCompletedSuccessfully && ServiceHandle.Instance != null)
            {
                var releaseTask = ServiceActivator.ReleaseAsync(ServiceHandle);
                if (!releaseTask.IsCompletedSuccessfully)
                {
                    return AwaitServiceReleaseAndReturn(invokerTask.Result, ServiceHandle);
                }

                return invokerTask;
            }

            return AwaitInvoker(invokerTask, ServiceHandle);
        }
        
        private async Task<TResponse> AwaitInvoker(Task<TResponse> invokerTask, TrpcServiceHandle serviceHandle)
        {
            try
            {
                return await invokerTask;
            }
            finally
            {
                if (serviceHandle.Instance != null)
                {
                    await ServiceActivator.ReleaseAsync(serviceHandle);
                }
            }
        }
        
        private async Task<TResponse> AwaitServiceReleaseAndThrow(ValueTask releaseTask, ExceptionDispatchInfo ex)
        {
            await releaseTask;
            ex.Throw();
            
            // Should never reach here
            return null;
        }

        private async Task<TResponse> AwaitServiceReleaseAndReturn(TResponse invokerResult, TrpcServiceHandle serviceHandle)
        {
            await ServiceActivator.ReleaseAsync(serviceHandle);
            return invokerResult;
        }
    }

}