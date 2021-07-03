using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using TrpcSharp.Protocol;

namespace TrpcSharp.Server.Trpc
{
    public interface ITrpcApplication
    {
        void EnqueueRequest(TrpcContext trpcContext);
        TrpcContext CreateTrpcContext(ITrpcMessage incomingMessage, ConnectionContext connection);
    }
    
    internal class TrpcApplication : ITrpcApplication
    {
        private readonly ConcurrentQueue<TrpcContext> _requestQueue;
        private readonly TrpcRequestDelegate _requestDelegate;
        private readonly ILogger<TrpcApplication> _logger;

        public TrpcApplication(TrpcRequestDelegate requestDelegate, ILogger<TrpcApplication> logger)
        {
            _requestDelegate = requestDelegate;
            _logger = logger;
            _requestQueue = new ConcurrentQueue<TrpcContext>();
        }

        
        public void EnqueueRequest(TrpcContext trpcContext)
        {
            _requestQueue.Enqueue(trpcContext);
            HandleRequest();
        }

        public TrpcContext CreateTrpcContext(ITrpcMessage incomingMessage, ConnectionContext connection)
        {
            if (incomingMessage == null)
            {
                return null;
            }
            
            var context = new TrpcContext
            {
                Transport = connection.Transport,
            };
                
            switch (incomingMessage)
            {
                case UnaryRequestMessage unaryMsg:
                    context.UnaryRequest = unaryMsg;
                    context.Id = new ContextId() {Type = ContextType.UnaryRequest, Id = unaryMsg.RequestId};
                    break;
                case StreamMessage streamMsg:
                    context.StreamMessage = streamMsg;
                    context.Id = new ContextId() {Type = ContextType.StreamConnection, Id = streamMsg.StreamId};
                    break;
                default:
                    throw new ApplicationException($"Unsupported tRPC message type {incomingMessage.GetType()}");
            }
            
            _logger.LogDebug($"Request starting: {context.Id}");
            return context;
        }

        void HandleRequest()
        {
            while (_requestQueue.TryDequeue(out var ctx))
            {
                try
                {
                    var requestHandle = _requestDelegate(ctx);
                    requestHandle.ConfigureAwait(false);
                    requestHandle.Wait();

                    _logger.LogDebug($"Request complete: {ctx.Id}");
                }
                catch(Exception ex)
                {
                    _logger.LogError(EventIds.ApplicationError, ex, "tRPC Application Error");
                    // todo: send error response
                }
            }
        }
    }
}