using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace TrpcSharp.Server.TrpcServices.ServiceMethodCallers
{
    internal class UnaryServiceMethodCaller<TService, TRequest, TResponse> : ITrpcServiceMethodCaller
        where TService: class
        where TRequest: class
        where TResponse: class
    {
        private readonly Method<TRequest, TResponse> _methodDescriptor;
        private readonly TrpcUnaryMethod<TService, TRequest, TResponse> _methodExecutor;
        public UnaryServiceMethodCaller(Method<TRequest, TResponse> methodDescriptor, TrpcUnaryMethod<TService, TRequest, TResponse> methodExecutor)
        {
            _methodDescriptor = methodDescriptor;
            _methodExecutor = methodExecutor;
        }
        
        public Task CallServiceMethod(ITrpcServiceActivator serviceActivator, TrpcContext trpcContext)
        {
            var serviceHandle = serviceActivator.Create(trpcContext.Services, typeof(TService));
            if (serviceHandle.Instance == null)
            {
                throw new ApplicationException($"Could not initialize service instance for type {typeof(TService).FullName}");
            }
            
            var unaryContext = trpcContext as UnaryTrpcContext;
            Task<TResponse> invokerTask = null;
            try
            {
                TRequest request;
                if (typeof(TRequest) != typeof(Empty))
                {
                    byte[] bytes;
                    using(var memoryStream = new MemoryStream())
                    {
                        unaryContext!.Request.Data.CopyTo(memoryStream);
                        bytes = memoryStream.ToArray();
                    }
                    request = _methodDescriptor.RequestMarshaller.Deserializer(bytes);
                }
                else
                {
                    request = new Empty() as TRequest;
                }
               
                invokerTask = _methodExecutor(
                    serviceHandle.Instance as TService,
                    request,
                    unaryContext);
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
                    return WrapResponseTask(AwaitServiceReleaseAndReturn(invokerTask.Result, serviceActivator, serviceHandle), unaryContext);
                }

                return WrapResponseTask(invokerTask, unaryContext);
            }

            return WrapResponseTask(AwaitInvoker(invokerTask, serviceActivator, serviceHandle), unaryContext);
        }

        private async Task WrapResponseTask(Task<TResponse> methodInvokeTask, UnaryTrpcContext context)
        {
            await methodInvokeTask;
            TResponse returnValue = methodInvokeTask.Result;
            if (returnValue == null)
            {
                return;
            }

            byte[] bytes = _methodDescriptor.ResponseMarshaller.Serializer(returnValue);
            var memoryStream = new MemoryStream(bytes);
            context.Response.Data = memoryStream;
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