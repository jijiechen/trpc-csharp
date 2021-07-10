using System;
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
    public interface ITrpcMessageDispatcher
    {
        Task DispatchRequestAsync(ITrpcMessage trpcMessage, ConnectionContext conn);
    }
    
    internal class TrpcMessageDispatcher : ITrpcMessageDispatcher, IHostedService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ITrpcApplicationBuilder _appBuilder;
        private TrpcRequestDelegate _requestDelegate;
        private readonly GlobalStreamHolder _globalStreamHolder;
        private readonly ITrpcPacketFramer _trpcFramer;
        private readonly ILogger<TrpcMessageDispatcher> _logger;
        private volatile bool _isAppRunning = false;

        public TrpcMessageDispatcher(ITrpcApplicationBuilder appBuilder, ITrpcPacketFramer framer,
            IServiceScopeFactory serviceScopeFactory, ILogger<TrpcMessageDispatcher> logger)
        {
            _appBuilder = appBuilder;
            _trpcFramer = framer;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            
            _globalStreamHolder = new GlobalStreamHolder();
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
                            await HandleStreamDataMessage(trpcMessage as StreamDataMessage, conn.ConnectionId);
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
                    
                    if (context is UnaryTrpcContext {HasResponded: false} unaryCtx)
                    {
                        await unaryCtx.RespondAsync();
                    }
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

        private async Task HandleStreamDataMessage(StreamDataMessage dataMessage, string connectionId)
        {
            if (!_globalStreamHolder.TryGetStream(connectionId, dataMessage.StreamId, out var initCtx))
            {
                return;
            }

            if (initCtx.ReceiveChannel != null)
            {
                await initCtx.ReceiveChannel.Writer.WriteAsync(dataMessage!.Data);
            }
        }

        private async Task HandleStreamInitMessage(StreamTrpcContext ctx, IAsyncDisposable disposableScope)
        {
            if (_globalStreamHolder.TryGetStream(ctx.Connection.ConnectionId, ctx.Identifier.Id, out _))
            {
                throw new InvalidOperationException(
                    $"Duplicated stream id: tRPC {ctx.Identifier.Id} in connection {ctx.Connection.ConnectionId}");
            }

            _globalStreamHolder.AddStream(ctx);
            ctx.Connection.OnDisconnectedAsync(async (conn) =>
            {
                _globalStreamHolder.TryRemoveConnection(conn.ConnectionId);
            });

            try
            {
                _logger.LogInformation(EventIds.StreamInitialization,
                    $"Stream init message message received: tRPC {ctx.Identifier.Id} in connection {ctx.Connection.ConnectionId}");

                var initMessage = ctx.StreamMessage as StreamInitMessage;
               
                var handler = _requestDelegate(ctx);
                var output = ctx.FlushAllAsync();
                
                await Task.WhenAll(handler, output);

                switch (ctx.StreamingMode)
                {
                    case null:
                        await (ctx as IStreamCallTracker).RespondInitMessageAsync(TrpcRetCode.TrpcServerNoserviceErr);
                        break;
                    case TrpcServerStreamingMode.ClientStreaming:
                    {
                        var closeMessage = CreateStreamCloseResponse(initMessage,
                            TrpcStreamCloseType.TrpcStreamClose, TrpcRetCode.TrpcInvokeSuccess, null);
                        await ctx.WriteAsync(closeMessage);
                        break;
                    }
                }
                _globalStreamHolder.TryRemoveStream(ctx.Connection.ConnectionId, ctx.Identifier.Id);
            }
            finally
            {
                try
                {
                    await disposableScope.DisposeAsync();
                }
                catch
                {
                    // we can't to anything when service provider fails to dispose
                }
            }
        }

        private void HandleStreamFeedbackMessage(StreamFeedbackMessage feedbackMessage, string connectionId)
        {
            if (feedbackMessage == null)
            {
                return;
            }
            
            if (!_globalStreamHolder.TryGetStream(connectionId, feedbackMessage.StreamId, out var initStreamCtx))
            {
                throw new InvalidOperationException(
                    $"Stream id not found on server: tRPC {feedbackMessage.StreamId} in connection {connectionId}");
            }
            
            _logger.LogInformation(EventIds.WindowSizeIncrement,  
                $"Window size increment feedback message received: increment {feedbackMessage.WindowSizeIncrement} for tRPC {feedbackMessage.StreamId} in connection {connectionId}");
            (initStreamCtx as IStreamCallTracker).IncrementSendWindowSize(
                feedbackMessage.StreamId, feedbackMessage.WindowSizeIncrement);
        }

        private async Task HandleStreamCloseMessage(StreamCloseMessage closeMessage, string connectionId)
        {
            if (closeMessage == null)
            {
                return;
            }
            
            if (!_globalStreamHolder.TryGetStream(connectionId, closeMessage.StreamId, out var initCtx))
            {
                // the stream may already closed!
                return;
            }
            
            _logger.LogInformation(EventIds.ConnectionClose,  
                $"Close message received, connection closing: tRPC {closeMessage.StreamId} in connection {connectionId}");

            initCtx.ReceiveChannel?.Writer.TryComplete();
            initCtx.SendChannel?.Writer.TryComplete();
            if (closeMessage.CloseType == TrpcStreamCloseType.TrpcStreamClose && initCtx.SendChannel != null)
            {
                await initCtx.SendChannel.Reader.Completion;
            }

            // stream/connection 相关的清理工作，会在 Connection.Disconnect 事件里处理
            await initCtx.Connection.AbortAsync();
        }

        private async Task SendErrorResponse(TrpcContext ctx)
        {
            try
            {
                if (ctx is StreamTrpcContext streamCtx)
                {
                    if (!(streamCtx.StreamMessage is StreamInitMessage))
                    {
                        if (!_globalStreamHolder.TryGetStream(streamCtx.Connection.ConnectionId, streamCtx.Identifier.Id, out streamCtx))
                        {
                            return;
                        }
                    }

                    var initMessage = streamCtx.StreamMessage as StreamInitMessage;
                    var closeMessage = CreateStreamCloseResponse(initMessage, TrpcStreamCloseType.TrpcStreamReset,
                        TrpcRetCode.TrpcServerSystemErr, "Internal Server Error");
                    await streamCtx.WriteAsync(closeMessage);
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
              
                    await _trpcFramer.WriteMessageAsync(unaryCtx.UnaryResponse, 
                        ctx.Connection.Transport.Output.AsStream(leaveOpen: true));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(EventIds.ApplicationError, ex, 
                    $"Error sending error response: tRPC {ctx.Identifier} in connection {ctx.Connection.ConnectionId}");
            }
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
        
        private static StreamCloseMessage CreateStreamCloseResponse(StreamInitMessage initMsg, 
            TrpcStreamCloseType closeType, TrpcRetCode returnCode, string message)
        {
            var closeMessage = new StreamCloseMessage
            {
                StreamId = initMsg.StreamId,
                CloseType = closeType,
                ReturnCode = returnCode,
                Message = message
            };
            
            // forward special TransInfo
            initMsg.RequestMeta?.AdditionalData.Keys
                .Where(k => k.StartsWith("trpc-"))
                .ToList()
                .ForEach(key =>
                {
                    closeMessage.AdditionalData[key] = initMsg.RequestMeta.AdditionalData[key];
                });

            return closeMessage;
        }
    }
}