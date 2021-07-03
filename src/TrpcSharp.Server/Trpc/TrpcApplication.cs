using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrpcSharp.Protocol;
using TrpcSharp.Protocol.Framing;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Server.Trpc
{
    public interface ITrpcApplication
    {
        void EnqueueRequest(TrpcContext trpcContext);
        TrpcContext CreateTrpcContext(ITrpcMessage incomingMessage, ConnectionContext connection);
    }
    
    internal class TrpcApplication : ITrpcApplication, IHostedService
    {
        private readonly ITrpcApplicationBuilder _appBuilder;
        private readonly ConcurrentQueue<TrpcContext> _requestQueue;
        private TrpcRequestDelegate _requestDelegate;
        private readonly ITrpcPacketFramer _trpcFramer;
        private readonly ILogger<TrpcApplication> _logger;
        private volatile bool _isAppRunning = false;

        public TrpcApplication(ITrpcApplicationBuilder appBuilder, ITrpcPacketFramer framer,  ILogger<TrpcApplication> logger)
        {
            _appBuilder = appBuilder;
            _trpcFramer = framer;
            _logger = logger;
            _requestQueue = new ConcurrentQueue<TrpcContext>();
        }


        public Task StartAsync(CancellationToken cancellationToken)
        {
            _requestDelegate = _appBuilder.Build();
            _isAppRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _isAppRunning = false;
            return Task.CompletedTask;
        }

        public void EnqueueRequest(TrpcContext trpcContext)
        {
            _requestQueue.Enqueue(trpcContext);
            HandleRequest();
        }

        public TrpcContext CreateTrpcContext(ITrpcMessage incomingMessage, ConnectionContext connection)
        {
            if (!_isAppRunning || incomingMessage == null)
            {
                return null;
            }
            
            TrpcContext context = null;
            switch (incomingMessage)
            {
                case UnaryRequestMessage unaryMsg:
                    context = new UnaryTrpcContext
                    {
                        Transport = connection.Transport,
                        Id = new ContextId() {Type = ContextType.UnaryRequest, Id = unaryMsg.RequestId},
                        UnaryRequest = unaryMsg,
                        UnaryResponse = CreateResponse(unaryMsg)
                    };
                    break;
                case StreamMessage streamMsg:
                    context = new StreamTrpcContext(_trpcFramer)
                    {
                        Transport = connection.Transport,
                        Id = new ContextId() {Type = ContextType.StreamConnection, Id = streamMsg.StreamId},
                        StreamMessage = streamMsg
                    };
                    break;
                default:
                    throw new ApplicationException($"Unsupported tRPC message type {incomingMessage.GetType()}");
            }
            
            _logger.LogDebug($"tRPC starting: {context.Id}");
            return context;
        }

        private static UnaryResponseMessage CreateResponse(UnaryRequestMessage unaryMsg)
        {
            var response = new UnaryResponseMessage
            {
                RequestId = unaryMsg.RequestId,
                CallType = unaryMsg.CallType
            };
            
            // forward special TransInfo
            unaryMsg.AdditionalData.Keys
                .Where(k => k.StartsWith("trpc-"))
                .ToList()
                .ForEach(key =>
                {
                    response.AdditionalData[key] = unaryMsg.AdditionalData[key];
                });

            return response;
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

                    if (ctx is UnaryTrpcContext unaryCtx && unaryCtx.UnaryRequest.CallType == TrpcCallType.TrpcUnaryCall)
                    {
                        _trpcFramer.WriteMessage(unaryCtx.UnaryResponse, ctx.Transport.Output);
                    }
                    _logger.LogDebug($"tRPC complete: {ctx.Id}");
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