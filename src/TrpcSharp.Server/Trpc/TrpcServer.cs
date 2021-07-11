using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrpcSharp.Protocol.Framing;

namespace TrpcSharp.Server.Trpc
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
                _logger.LogDebug($"New connection {connection.ConnectionId} established from remote endpoint {connection.RemoteEndPoint}");
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
                    _logger.LogDebug($"Connection {connection.ConnectionId} reset from remote endpoint {connection.RemoteEndPoint}");
                }
                finally
                {
                    await connection.Transport.Input.CompleteAsync();
                    await connection.Transport.Output.CompleteAsync();
                }
            }

            private async Task<bool> ReadAndProcessMessage(ConnectionContext connection, ReadResult result, PipeReader input)
            {
                try
                {
                    if (result.IsCanceled)
                    {
                        return false;
                    }

                    var buffer = result.Buffer;
                    if (!buffer.IsEmpty)
                    {
                        var advanced = false;
                        var consumed = buffer.Start;
                        SequencePosition examined;
                        while (_framer.TryReadMessageAsServer(buffer.Slice(consumed), out var message, out consumed, out examined))
                        {
                            input.AdvanceTo(consumed, examined);
                            advanced = true;

                            try
                            {
                                await _messageDispatcher.DispatchRequestAsync(message, connection);
                            }
                            catch (OperationCanceledException)
                            {
                                // Don't treat OperationCanceledException as an error, it's basically a "control flow"
                                // exception to stop things from running
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(EventIds.ApplicationError, ex, "Unhandled application exception: " + ex.Message);
                                break;
                            }
                        }

                        if (!advanced)
                        {
                            input.AdvanceTo(consumed, examined);
                        }
                    }

                    if (result.IsCompleted)
                    {
                        if (!buffer.IsEmpty)
                        {
                            throw new InvalidDataException("Connection terminated while reading a message.");
                        }

                        return false;
                    }
                    
                    return true;
                }
                catch (InvalidDataException dataEx)
                {
                    _logger.LogDebug(EventIds.ProtocolError, dataEx.Message);
                    return false;
                }
                catch (ConnectionResetException closeEx)
                {
                    _logger.LogDebug(EventIds.ConnectionReset, closeEx.Message);
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(EventIds.UnknownConnectionError, ex.Message);
                    return false;
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