using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrpcSharp.Protocol;
using TrpcSharp.Protocol.Framing;

namespace TrpcSharp.Server
{
    public class TrpcServerOptionsSetup : IConfigureOptions<KestrelServerOptions>
    {
        private readonly ServerOptions _options;
        public TrpcServerOptionsSetup(IOptions<ServerOptions> options)
        {
            _options = options.Value;
        }

        public void Configure(KestrelServerOptions options)
        {
            options.Listen(_options.EndPoint, builder =>
            {
                builder.UseConnectionHandler<TrpcConnectionHandler>();
            });
        }

        private class TrpcConnectionHandler : ConnectionHandler
        {
            private readonly ITrpcPacketFramer _framer;
            private readonly ITrpcMessageDispatcher _messageDispatcher;
            private readonly ILogger<TrpcConnectionHandler> _logger;

            public TrpcConnectionHandler(ITrpcPacketFramer framer, ITrpcMessageDispatcher messageDispatcher, 
                ILogger<TrpcConnectionHandler> logger)
            {
                _framer = framer;
                _messageDispatcher = messageDispatcher;
                _logger = logger;
            }

            public override async Task OnConnectedAsync(ConnectionContext connection)
            {
                _logger.LogDebug(EventIds.ConnectionEstablished, $"Connection established: {connection.ConnectionId} from remote endpoint {connection.RemoteEndPoint}");
                try
                {
                    var input = connection.Transport.Input;
                    while (true)
                    {
                        var result = await input.ReadAsync();
                        var shouldContinue = await ReadAndProcessMessage(connection, result, input);
                        if (!shouldContinue)
                            break;
                    }
                }
                catch (ConnectionResetException)
                {
                    _logger.LogDebug(EventIds.ConnectionReset,$"Connection {connection.ConnectionId} reset from remote endpoint {connection.RemoteEndPoint}");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(EventIds.UnknownConnectionError, $"Error in connection {connection.ConnectionId}: {ex.Message}");
                }
                finally
                {
                    await connection.Transport.Input.CompleteAsync();
                    await connection.Transport.Output.CompleteAsync();
                    
                    _logger.LogDebug(EventIds.ConnectionClose, $"Connection closed: {connection.ConnectionId} from remote endpoint {connection.RemoteEndPoint}");
                }
            }

            private async Task<bool> ReadAndProcessMessage(ConnectionContext connection, ReadResult result, PipeReader input)
            {
                var connectionId = connection.ConnectionId;
                try
                {
                    if (result.IsCanceled)
                    {
                        return false;
                    }

                    var buffer = result.Buffer;
                    if (!buffer.IsEmpty)
                    {
                        if (_framer.TryReadMessageAsServer(buffer, out var message, out long msgDataLength, 
                            out var consumed, out var examined))
                        {
                            input.AdvanceTo(consumed, examined);
                            try
                            {
                                var hasBody = msgDataLength > 0;
                                Stream dataStream = null;
                                if (hasBody)
                                {
                                    dataStream = new TrpcDataStream(input, msgDataLength);
                                    message.SetMessageData(dataStream);
                                }
                                
                                await _messageDispatcher.DispatchRequestAsync(message, connection);
                                await ConsumeMessageData(dataStream, connectionId);
                            }
                            catch (OperationCanceledException)
                            {
                                // Don't treat OperationCanceledException as an error, it's basically a "control flow"
                                // exception to stop things from running
                            }
                            catch (Exception ex) when (ex is not ConnectionAbortedException && ex is not ConnectionResetException)
                            {
                                _logger.LogWarning(EventIds.ApplicationError, ex, 
                                    $"Unhandled application exception in connection {connectionId}: " + ex.Message);
                            }
                        }
                        else
                        {
                            input.AdvanceTo(consumed, examined);
                        }
                    }

                    if (result.IsCompleted)
                    {
                        if (!buffer.IsEmpty)
                        {
                            throw new InvalidDataException($"Connection terminated while reading a message in connection {connectionId}.");
                        }

                        return false;
                    }
                    
                    return true;
                }
                catch (InvalidDataException dataEx)
                {
                    _logger.LogDebug(EventIds.ProtocolError, $"Invalid data in connection {connectionId}: {dataEx.Message}");
                    return false;
                }
            }

            /// <summary>
            /// Consume any remaining body to complete this invocation
            /// </summary>
            private async Task ConsumeMessageData(Stream bodyStream, string connectionId)
            {
                if (bodyStream == null)
                {
                    return;
                }
                
                try
                {
                    await bodyStream.CopyToAsync(Stream.Null);
                }
                catch (ObjectDisposedException)
                {
                    // this stream has been disposed/consumed by application, ignore it
                }
                catch (OperationCanceledException ex) when (ex is ConnectionAbortedException || ex is TaskCanceledException)
                {
                    _logger.LogDebug(EventIds.ErrorDrainingMessageData, $"Error draining message data in {connectionId}: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    throw new ConnectionAbortedException("Application aborted the connection", ex);
                }
            }
        }
    }
    
    public class ServerOptions
    {
        public IPEndPoint EndPoint { get; set; }
        // certificate, etc.
    }
}