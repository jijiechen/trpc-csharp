using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrpcSharp.Protocol;
using TrpcSharp.Protocol.Framing;
using TrpcSharp.Protocol.Standard;
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
        private TrpcRequestDelegate _requestDelegate;
        private readonly StreamTracker _streamTracker;
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
            
            _streamTracker = new StreamTracker();
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
            // _streamTracker.TryClearStreams(xxxx)
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
                    var streamCtx = context as StreamTrpcContext;
                    switch (streamCtx!.StreamMessage.StreamFrameType)
                    {
                        case TrpcStreamFrameType.TrpcStreamFrameInit:
                            await HandleStreamInitMessage(streamCtx, scope);
                            break;
                        case TrpcStreamFrameType.TrpcStreamFrameData:
                            // Thread.Channels!
                            break;
                        case TrpcStreamFrameType.TrpcStreamFrameFeedback:
                            HandleStreamFeedbackMessage(trpcMessage as StreamFeedbackMessage, conn.ConnectionId);
                            break;
                        case TrpcStreamFrameType.TrpcStreamFrameClose:
                            await HandleStreamCloseMessage(trpcMessage as StreamCloseMessage, conn.ConnectionId);
                            break;
                    }
                }
                else
                {
                    var requestHandle = _requestDelegate(context);
                    await requestHandle.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(EventIds.ApplicationError, ex, $"Application error: tRPC {context.Identifier} in connection {conn.ConnectionId}");
                await SendErrorResponse(context);
            }
            finally
            {
                _logger.LogDebug($"Process completed: tRPC {context.Identifier} in connection {conn.ConnectionId}");
                if (scope != null)
                {
                    await scope.DisposeAsync();
                }
            }
        }

        private async Task SendErrorResponse(TrpcContext ctx)
        {
            try
            {
                var outputStream = ctx.Connection.Transport.Output.AsStream(leaveOpen: true);
                if (ctx is StreamTrpcContext streamCtx)
                {
                    var resp = new StreamInitResponseMeta
                    {
                        ReturnCode = TrpcRetCode.TrpcServerSystemErr,
                        ErrorMessage = "Internal Server Error"
                    };
                    await SendInitResponse(streamCtx, resp);
                }
                else
                {
                    var unaryCtx = ctx as UnaryTrpcContext;
                    if (unaryCtx == null)
                    {
                        // we can't recognize this context
                        return;
                    }

                    unaryCtx.UnaryResponse = CreateResponse(unaryCtx.UnaryRequest);
                    unaryCtx.UnaryResponse.ReturnCode = TrpcRetCode.TrpcServerSystemErr;
                    unaryCtx.UnaryResponse.ErrorMessage = "Internal Server Error";
              
                    await _trpcFramer.WriteMessageAsync(unaryCtx.UnaryResponse, outputStream);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(EventIds.ApplicationError, ex, 
                    $"Error sending error response: tRPC {ctx.Identifier} in connection {ctx.Connection.ConnectionId}");
            }
        }

        private async Task HandleStreamInitMessage(StreamTrpcContext ctx, IAsyncDisposable disposableScope)
        {
            if (_streamTracker.TryGetStream(ctx.Connection.ConnectionId, ctx.Identifier.Id, out _))
            {
                throw new InvalidOperationException(
                    $"Duplicated stream id: tRPC {ctx.Identifier.Id} in connection {ctx.Connection.ConnectionId}");
            }

            ctx.Connection.OnDisconnectedAsync(async (conn) =>
            {
                await _streamTracker.TryClearStreams(conn.ConnectionId);
                await disposableScope.DisposeAsync();
            });

            _logger.LogInformation(EventIds.StreamInitialization,  
                $"Stream init message message received: tRPC {ctx.Identifier.Id} in connection {ctx.Connection.ConnectionId}");

            var requestHandle = _requestDelegate(ctx);
            await requestHandle.ConfigureAwait(false);
            
            await SendInitResponse(ctx, null);
        }

        private async Task SendInitResponse(StreamTrpcContext ctx, StreamInitResponseMeta response)
        {
            var initResponse = new StreamInitMessage
            {
                StreamId = ctx.Identifier.Id,
                ContentType = TrpcContentEncodeType.TrpcProtoEncode,
                ContentEncoding = TrpcCompressType.TrpcDefaultCompress,
                ResponseMeta = response
            };

            await ctx.WriteAsync(initResponse);
        }

        private void HandleStreamFeedbackMessage(StreamFeedbackMessage feedbackMessage, string connectionId)
        {
            if (feedbackMessage == null)
            {
                return;
            }
            
            if (!_streamTracker.TryGetStream(connectionId, feedbackMessage.StreamId, out var initStreamCtx))
            {
                throw new InvalidOperationException(
                    $"Stream id not found on server: tRPC {feedbackMessage.StreamId} in connection {connectionId}");
            }
            
            _logger.LogInformation(EventIds.WindowSizeIncrement,  
                $"Window size increment feedback message received: increment {feedbackMessage.WindowSizeIncrement} for tRPC {feedbackMessage.StreamId} in connection {connectionId}");
            initStreamCtx.IncrementWindowSize(feedbackMessage.WindowSizeIncrement);
        }

        private async Task HandleStreamCloseMessage(StreamCloseMessage closeMessage, string connectionId)
        {
            if (closeMessage == null)
            {
                return;
            }
            
            if (!_streamTracker.TryGetStream(connectionId, closeMessage.StreamId, out var initStreamCtx))
            {
                // the stream may already closed!
                return;
            }
            _logger.LogInformation(EventIds.ConnectionClose,  
                $"Close message received, connection closing: tRPC {closeMessage.StreamId} in connection {connectionId}");

            var pendingWrite = false;
            var closeType = closeMessage.CloseType;
            if (closeType == TrpcStreamCloseType.TrpcStreamClose && !pendingWrite)
            {
                // todo: 发出去
            }
            await initStreamCtx.Connection.AbortAsync();
        }

        private (TrpcContext, IAsyncDisposable) CreateTrpcContext(ITrpcMessage incomingMessage, ConnectionContext connection)
        {
            if (!_isAppRunning || incomingMessage == null)
            {
                return (null, null);
            }
            
            AsyncServiceScope scope;
            TrpcContext context = null;
            switch (incomingMessage)
            {
                case UnaryRequestMessage unaryMsg:
                    scope = _serviceScopeFactory.CreateAsyncScope();
                    context = new UnaryTrpcContext(unaryMsg.RequestId, _trpcFramer)
                    {
                        Services = scope.ServiceProvider,
                        Connection = new AspNetCoreConnection(connection),
                        UnaryRequest = unaryMsg,
                        UnaryResponse = CreateResponse(unaryMsg)
                    };
                    break;
                case StreamMessage streamMsg:
                    var isInitMessage = streamMsg.StreamFrameType == TrpcStreamFrameType.TrpcStreamFrameInit;
                    scope = isInitMessage ? _serviceScopeFactory.CreateAsyncScope() : null;
                    context = new StreamTrpcContext(streamMsg.StreamId, _trpcFramer)
                    {
                        Services = scope?.ServiceProvider,
                        Connection = new AspNetCoreConnection(connection),
                        StreamMessage = streamMsg
                    };
                    break;
                default:
                    throw new ApplicationException($"Unsupported message type {incomingMessage.GetType()}: connection {connection.ConnectionId}");
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