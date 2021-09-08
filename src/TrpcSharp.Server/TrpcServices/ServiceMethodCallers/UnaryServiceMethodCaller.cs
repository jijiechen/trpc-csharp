using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace TrpcSharp.Server.TrpcServices.ServiceMethodCallers
{
    internal class UnaryServiceMethodCaller<TService, TRequest, TResponse> : ITrpcServiceMethodCaller
        where TService: class
        where TRequest: class
        where TResponse: class
    {
        private readonly TrpcUnaryMethod<TService, TRequest, TResponse> _methodExecutor;
        public UnaryServiceMethodCaller(TrpcUnaryMethod<TService, TRequest, TResponse> methodExecutor)
        {
            _methodExecutor = methodExecutor;
        }
        
        public Task CallServiceMethod(ITrpcServiceActivator serviceActivator, TrpcContext trpcContext)
        {
            var serviceHandle = serviceActivator.Create(trpcContext.Services, typeof(TService));
            if (serviceHandle.Instance == null)
            {
                throw new ApplicationException($"Could not initialize service instance for type {typeof(TService).FullName}");
            }
            
            Task<TResponse> invokerTask = null;
            try
            {
                var unaryContext = trpcContext as UnaryTrpcContext;

                var request = unaryContext.Request; 
                // var response = unaryContext.Response;
                
                invokerTask = _methodExecutor(
                    serviceHandle.Instance as TService,
                    request as TRequest,
                    unaryContext);
                
                // unaryContext.Response.Data
            }
            catch (Exception ex)
            {
                // Invoker calls user code. User code may throw an exception instead
                // of a faulted task. We need to catch the exception, ensure cleanup
                // runs and convert exception into a faulted task.
                if (serviceHandle.Instance != null)
                {
                    var releaseTask = serviceActivator.ReleaseAsync(serviceHandle);
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

            if (invokerTask.IsCompletedSuccessfully && serviceHandle.Instance != null)
            {
                var releaseTask = serviceActivator.ReleaseAsync(serviceHandle);
                if (!releaseTask.IsCompletedSuccessfully)
                {
                    return AwaitServiceReleaseAndReturn(invokerTask.Result, serviceActivator, serviceHandle);
                }

                return invokerTask;
            }

            return AwaitInvoker(invokerTask, serviceActivator, serviceHandle);
        }
        
        private async Task<TResponse> AwaitInvoker(Task<TResponse> invokerTask, ITrpcServiceActivator serviceActivator, TrpcServiceHandle serviceHandle)
        {
            try
            {
                return await invokerTask;
            }
            finally
            {
                if (serviceHandle.Instance != null)
                {
                    await serviceActivator.ReleaseAsync(serviceHandle);
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

        private async Task<TResponse> AwaitServiceReleaseAndReturn(TResponse invokerResult, ITrpcServiceActivator serviceActivator, TrpcServiceHandle serviceHandle)
        {
            await serviceActivator.ReleaseAsync(serviceHandle);
            return invokerResult;
        }

        
    }

}