using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrpcSharp.Protocol;
using TrpcSharp.Protocol.Framing;
using TrpcSharp.Server.Extensions;

namespace TrpcSharp.Server.Trpc
{
    public interface ITrpcApplication
    {
        Task DispatchRequestAsync(ITrpcMessage trpcMessage, ConnectionContext conn);
    }
    
    internal class TrpcApplication : ITrpcApplication, IHostedService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ITrpcApplicationBuilder _appBuilder;
        private readonly ConcurrentQueue<TrpcContext> _requestQueue;
        private TrpcRequestDelegate _requestDelegate;
        private readonly ITrpcPacketFramer _trpcFramer;
        private readonly ILogger<TrpcApplication> _logger;
        private volatile bool _isAppRunning = false;

        public TrpcApplication(ITrpcApplicationBuilder appBuilder, ITrpcPacketFramer framer,
            IServiceScopeFactory serviceScopeFactory, ILogger<TrpcApplication> logger)
        {
            _appBuilder = appBuilder;
            _trpcFramer = framer;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
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
            // todo: cleanup ongoing connections
            return Task.CompletedTask;
        }

        public async Task DispatchRequestAsync(ITrpcMessage trpcMessage, ConnectionContext conn)
        {
            var (context, scope) = CreateTrpcContext(trpcMessage, conn);
            if (context == null)
            {
                _logger.LogDebug($"A tRPC context can not be created for connection {conn.ConnectionId}");
                return;
            }
            
            try
            {
                _logger.LogDebug($"tRPC {context.Identifier} starting in connection {conn.ConnectionId}");

                if (context.Identifier.Type == ContextType.Streaming)
                {
                    // stream tracker!
                    // init
                    // close
                }
                else
                {
                    var requestHandle = _requestDelegate(context);
                    await requestHandle.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(EventIds.ApplicationError, ex, "tRPC Application Error");
                // todo: send error response
            }
            finally
            {
                _logger.LogDebug($"tRPC {context.Identifier} completed in connection {conn.ConnectionId}");
                await scope.DisposeAsync();
            }
        }

        private (TrpcContext, IAsyncDisposable) CreateTrpcContext(ITrpcMessage incomingMessage, ConnectionContext connection)
        {
            if (!_isAppRunning || incomingMessage == null)
            {
                return (null, null);
            }
            
            var scope = _serviceScopeFactory.CreateAsyncScope();
            TrpcContext context = null;
            switch (incomingMessage)
            {
                case UnaryRequestMessage unaryMsg:
                    context = new UnaryTrpcContext(unaryMsg.RequestId, _trpcFramer)
                    {
                        Services = scope.ServiceProvider,
                        Connection = new AspNetCoreConnection(connection),
                        UnaryRequest = unaryMsg,
                        UnaryResponse = CreateResponse(unaryMsg)
                    };
                    break;
                case StreamMessage streamMsg:
                    context = new StreamTrpcContext(streamMsg.StreamId, _trpcFramer)
                    {
                        Services = scope.ServiceProvider,
                        Connection = new AspNetCoreConnection(connection),
                        StreamMessage = streamMsg
                    };
                    break;
                default:
                    throw new ApplicationException($"Unsupported tRPC message type {incomingMessage.GetType()}");
            }
            
            return (context, scope);
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

    }
}