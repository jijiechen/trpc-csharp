﻿using System;
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
using TrpcSharp.Server.Exceptions;
using TrpcSharp.Server.Extensions;

namespace TrpcSharp.Server
{
    public interface ITrpcMessageDispatcher
    {
        Task DispatchRequestAsync(ITrpcMessage trpcMessage, ConnectionContext conn);
    }
    
    internal class TrpcMessageDispatcher : ITrpcMessageDispatcher, IHostedService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITrpcApplicationBuilder _appBuilder;
        private TrpcRequestDelegate _requestDelegate;
        private readonly GlobalStreamHolder _globalStreamHolder;
        private readonly ITrpcPacketFramer _trpcFramer;
        private readonly ILogger<TrpcMessageDispatcher> _logger;
        private volatile bool _isAppRunning = false;

        public TrpcMessageDispatcher(IServiceProvider serviceProvider,
            ITrpcApplicationBuilder appBuilder, ITrpcPacketFramer framer,
            IServiceScopeFactory serviceScopeFactory, ILogger<TrpcMessageDispatcher> logger)
        {
            _serviceProvider = serviceProvider;
            _appBuilder = appBuilder;
            _trpcFramer = framer;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            
            _globalStreamHolder = new GlobalStreamHolder();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _requestDelegate = _appBuilder.Build(_serviceProvider);
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
            var (ctxId, context, scope) = CreateTrpcContext(trpcMessage, conn);
            if (ctxId.Id == 0)
            {
                _logger.LogWarning($"A tRPC context can not be created for connection {conn.ConnectionId}");
                return;
            }

            if (context.Identifier.Type == ContextType.Streaming)
            {
                try
                {
                    var streamCtx = context as StreamTrpcContext;
                    var streamMessage = trpcMessage as StreamMessage;
                    switch (streamMessage!.StreamFrameType)
                    {
                        case TrpcStreamFrameType.TrpcStreamFrameInit:
                            await HandleStreamInitMessage(streamCtx, scope);
                            break;
                        case TrpcStreamFrameType.TrpcStreamFrameData:
                            await HandleStreamDataMessage(ctxId, trpcMessage as StreamDataMessage);
                            break;
                        case TrpcStreamFrameType.TrpcStreamFrameFeedback:
                            HandleStreamFeedbackMessage(ctxId, trpcMessage as StreamFeedbackMessage);
                            break;
                        case TrpcStreamFrameType.TrpcStreamFrameClose:
                            await HandleStreamCloseMessage(ctxId, trpcMessage as StreamCloseMessage);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(EventIds.ApplicationError, ex, $"{context.Identifier} Application error.");
                    await SendErrorResponse(context);
                }
            }
            else
            {
                var unaryCtx = context as UnaryTrpcContext;
                
                try
                {
                    _logger.LogInformation(EventIds.RpcStarting, $"{context.Identifier} tRPC starting");
                    var requestHandle = _requestDelegate(context);
                    await requestHandle.ConfigureAwait(false);

                    if (!unaryCtx!.HasResponded)
                    {
                        await unaryCtx.RespondAsync();
                    }
                }
                catch (TrpcCompletedException complete)
                {
                    await CompleteUnaryByApplication(unaryCtx, complete);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(EventIds.ApplicationError, ex, $"{context.Identifier} Application error.");
                    await SendErrorResponse(context);
                }
                finally
                {
                    _logger.LogInformation(EventIds.RpcCompleted, $"{context.Identifier} tRPC completed.");
                    if (scope != null)
                    {
                        await scope.DisposeAsync();
                    }
                }
            }
        }

        private async Task HandleStreamInitMessage(StreamTrpcContext ctx, IAsyncDisposable disposableScope)
        {
            if (_globalStreamHolder.TryGetStream(ctx.Connection.ConnectionId, ctx.Identifier.Id, out _))
            {
                var message = $"{ctx.Identifier} Duplicated stream id rejected.";
                _logger.LogWarning(EventIds.StreamIdDuplicated, message);
                throw new InvalidOperationException(message);
            }
            _logger.LogInformation(EventIds.RpcStarting, $"{ctx.Identifier} tRPC starting");

            _globalStreamHolder.AddStream(ctx);
            ctx.Connection.OnDisconnectedAsync((conn) =>
            {
                var _ = _globalStreamHolder.TryRemoveConnection(conn.ConnectionId);
            });
            (ctx as IStreamCallTracker).OnComplete(async (c, closeType) =>
            {
                await CompleteStreamInvocation(c, closeType, disposableScope);
            });

            var initResponseTask = (ctx as IStreamCallTracker).GetInitResponseTask(ctx.Identifier.Id);
            var __ = StartStreamInvocation(ctx);
            await initResponseTask;
        }

        private async Task StartStreamInvocation(StreamTrpcContext ctx)
        {
            try
            {
                var handler = _requestDelegate(ctx);
                var output = ctx.SendAsync();

                await Task.WhenAll(handler, output);
                await ctx.FlushAsync();

                if (ctx.StreamingMode == null)
                {
                    await (ctx as IStreamCallTracker).RespondInitMessageAsync(ctx.Identifier.Id,
                        TrpcRetCode.TrpcServerNoserviceErr);
                }
                else
                {
                    var closeMessage = CreateStreamCloseResponse(ctx.InitMessage,
                        TrpcStreamCloseType.TrpcStreamClose, TrpcRetCode.TrpcInvokeSuccess, null);
                    await ctx.WriteAsync(closeMessage);
                }
                
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
            catch (TrpcCompletedException complete)
            {
                await CompleteStreamByApplication(ctx, complete);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(EventIds.ApplicationError, ex, $"{ctx.Identifier} Application error.");
                await SendErrorResponse(ctx);
            }
            finally
            {
                await _globalStreamHolder.TryRemoveStream(ctx.Identifier.ConnectionId, ctx.Identifier.Id, TrpcStreamCloseType.TrpcStreamClose);
            }
        }

        private async Task HandleStreamDataMessage(ContextId ctxId, StreamDataMessage dataMessage)
        {
            if (!_globalStreamHolder.TryGetStream(ctxId.ConnectionId, ctxId.Id, out var initCtx))
            {
                _logger.LogDebug(EventIds.StreamIdNotFound, $"{ctxId} Stream id not found on server when handle feedback message.");
                return;
            }

            _logger.LogInformation(EventIds.StreamDataReceived, $"{ctxId} Stream data message received.");
            if (initCtx.ReceiveChannel != null)
            {
                await initCtx.ReceiveChannel.Writer.WriteAsync(dataMessage!.Data);
                if (dataMessage.Data is TrpcDataStream messageData)
                {
                    await messageData.AccessByApp;
                }
            }
        }

        private void HandleStreamFeedbackMessage(ContextId ctxId, StreamFeedbackMessage feedbackMessage)
        {
            if (!_globalStreamHolder.TryGetStream(ctxId.ConnectionId, ctxId.Id, out var initStreamCtx))
            {
                _logger.LogDebug(EventIds.StreamIdNotFound, $"{ctxId} Stream id not found on server when handle feedback message.");
                return;
            }
            
            _logger.LogInformation(EventIds.StreamFeedbackReceived,  
                $"{ctxId} Stream feedback message received: increment {feedbackMessage.WindowSizeIncrement}");
            (initStreamCtx as IStreamCallTracker).IncrementSendWindowSize(
                feedbackMessage.StreamId, feedbackMessage.WindowSizeIncrement);
        }

        private async Task HandleStreamCloseMessage(ContextId ctxId, StreamCloseMessage closeMessage)
        {
            if (!_globalStreamHolder.TryGetStream(ctxId.ConnectionId, closeMessage.StreamId, out _))
            {
                _logger.LogDebug(EventIds.StreamIdNotFound, $"{ctxId} Stream id not found on server when handle close message.");
                return;
            }
            
            _logger.LogInformation(EventIds.StreamCloseReceived,  
                $"{ctxId} Stream close message received, stream closing.");
            await _globalStreamHolder.TryRemoveStream(ctxId.ConnectionId, ctxId.Id, closeMessage.CloseType);
        }

        private async Task SendErrorResponse(TrpcContext ctx)
        {
            try
            {
                if (ctx.Connection == null)
                {
                    return;
                }

                if (ctx is StreamTrpcContext streamCtx)
                {
                    var closeMessage = CreateStreamCloseResponse(streamCtx.InitMessage, 
                        TrpcStreamCloseType.TrpcStreamReset, TrpcRetCode.TrpcServerSystemErr, "Internal Server Error");
                    if (closeMessage != null)
                    {
                        await streamCtx.WriteAsync(closeMessage);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(3));
                    await _globalStreamHolder.TryRemoveStream(ctx.Identifier.ConnectionId, ctx.Identifier.Id, TrpcStreamCloseType.TrpcStreamReset);
                }
                else
                {
                    var unaryCtx = ctx as UnaryTrpcContext;
                    if (unaryCtx == null)
                    {
                        // we can't recognize this context
                        return;
                    }

                    unaryCtx.Response = CreateResponse(unaryCtx.Request);
                    unaryCtx.Response.ReturnCode = TrpcRetCode.TrpcServerSystemErr;
                    unaryCtx.Response.ErrorMessage = "Internal Server Error";
                    
                    await unaryCtx.RespondAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(EventIds.ApplicationError, ex,  $"{ctx.Identifier} Error sending error response.");
            }
        }

        private async Task CompleteUnaryByApplication(UnaryTrpcContext ctx, TrpcCompletedException completed)
        {
            if (completed.InnerException != null)
            {
                await SendErrorResponse(ctx);
                return;
            }

            try
            {
                ctx.Response = CreateResponse(ctx.Request);
                ctx.Response.ReturnCode = TrpcRetCode.TrpcInvokeSuccess;
                ctx.Response.ErrorMessage = "Call Completed";

                await ctx.RespondAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(EventIds.ApplicationError, ex, $"{ctx.Identifier} Error sending close message.");
            }
        }
        
        private async Task CompleteStreamByApplication(StreamTrpcContext ctx, TrpcCompletedException completed)
        {
            if (completed.InnerException != null)
            {
                await SendErrorResponse(ctx);
                return;
            }

            try
            {
                var closeMessage = CreateStreamCloseResponse(ctx.InitMessage,
                    TrpcStreamCloseType.TrpcStreamClose, TrpcRetCode.TrpcInvokeSuccess, null);
                await ctx.WriteAsync(closeMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(EventIds.ApplicationError, ex, $"{ctx.Identifier} Error sending close message.");
            }
        }

        private async Task CompleteStreamInvocation(StreamTrpcContext initCtx, TrpcStreamCloseType closeType, IAsyncDisposable disposableScope)
        {
            try
            {
                _logger.LogInformation(EventIds.RpcCompleted, $"{initCtx.Identifier} tRPC completed.");

                initCtx.ReceiveChannel?.Writer.TryComplete();
                initCtx.SendChannel?.Writer.TryComplete();
                if (closeType == TrpcStreamCloseType.TrpcStreamClose && initCtx.SendChannel != null)
                {
                    await initCtx.SendChannel.Reader.Completion;
                }

                var connection = initCtx.Connection;
                await disposableScope.DisposeAsync();
                if (closeType == TrpcStreamCloseType.TrpcStreamReset && connection != null)
                {
                    // stream/connection 相关的清理工作，会在 Connection.Disconnect 事件里处理
                    await connection.AbortAsync();
                }
            }
            catch(Exception ex)
            {
                _logger.LogWarning(EventIds.ApplicationError, ex, $"{initCtx} Error completing stream.");
            }
        }

        private (ContextId, TrpcContext, IAsyncDisposable) CreateTrpcContext(ITrpcMessage incomingMessage, ConnectionContext connection)
        {
            if (!_isAppRunning || incomingMessage == null)
            {
                return (default, null, null);
            }
            
            AsyncServiceScope scope = null;
            ContextId ctxId = default;
            TrpcContext context = null;
            switch (incomingMessage)
            {
                case UnaryRequestMessage unaryMsg:
                    scope = _serviceScopeFactory.CreateAsyncScope();
                    context = new UnaryTrpcContext(connection.ConnectionId, unaryMsg.RequestId, _trpcFramer)
                    {
                        Services = scope.ServiceProvider,
                        Connection = new AspNetCoreConnection(connection),
                        Request = unaryMsg,
                        Response = CreateResponse(unaryMsg)
                    };
                    ctxId = context.Identifier;
                    break;
                case StreamMessage streamMsg:
                    var isInitMessage = streamMsg.StreamFrameType == TrpcStreamFrameType.TrpcStreamFrameInit;
                    if (isInitMessage)
                    {
                        scope = _serviceScopeFactory.CreateAsyncScope();
                        context = new StreamTrpcContext(connection.ConnectionId, streamMsg.StreamId, _trpcFramer)
                        {
                            Services = scope.ServiceProvider,
                            Connection = new AspNetCoreConnection(connection),
                            InitMessage = streamMsg as StreamInitMessage
                        };
                        ctxId = context.Identifier;
                    }
                    else
                    {
                        ctxId = new ContextId{ Type = ContextType.Streaming, Id =  streamMsg.StreamId, ConnectionId = connection.ConnectionId};
                    }
                    break;
                default:
                    throw new ApplicationException($"Unsupported message type {incomingMessage.GetType()}: connection {connection.ConnectionId}");
            }
            
            return (ctxId, context, scope);
        }
        
        private static UnaryResponseMessage CreateResponse(UnaryRequestMessage unaryMsg)
        {
            var response = new UnaryResponseMessage
            {
                RequestId = unaryMsg.RequestId,
                CallType = unaryMsg.CallType
            };
            
            // forward special TransInfo
            unaryMsg.Metadata.Keys
                .Where(k => k.StartsWith("trpc-"))
                .ToList()
                .ForEach(key =>
                {
                    response.Metadata[key] = unaryMsg.Metadata[key];
                });

            return response;
        }
        
        private static StreamCloseMessage CreateStreamCloseResponse(StreamInitMessage initMsg, 
            TrpcStreamCloseType closeType, TrpcRetCode returnCode, string message)
        {
            if (initMsg == null)
            {
                return null;
            }
            
            var closeMessage = new StreamCloseMessage
            {
                StreamId = initMsg.StreamId,
                CloseType = closeType,
                ReturnCode = returnCode,
                Message = message
            };
            
            // forward special TransInfo
            initMsg.RequestMeta?.Metadata.Keys
                .Where(k => k.StartsWith("trpc-"))
                .ToList()
                .ForEach(key =>
                {
                    closeMessage.Metadata[key] = initMsg.RequestMeta.Metadata[key];
                });

            return closeMessage;
        }
    }
}