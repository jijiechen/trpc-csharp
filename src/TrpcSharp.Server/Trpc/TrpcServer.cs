using System;
using System.IO;
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
            private readonly ITrpcApplication _application;
            private readonly ILogger<TrpcConnectionHandler> _logger;

            public TrpcConnectionHandler(ITrpcPacketFramer framer, ITrpcApplication application, 
                ILogger<TrpcConnectionHandler> logger)
            {
                _framer = framer;
                _application = application;
                _logger = logger;
            }

            public override async Task OnConnectedAsync(ConnectionContext connection)
            {
                var input = connection.Transport.Input;
                while (true)
                {
                    var result = await input.ReadAsync();
                    var buffer = result.Buffer;

                    try
                    {
                        if (result.IsCanceled)
                        {
                            break;
                        }

                        if (!buffer.IsEmpty)
                        {
                            var advanced = false;
                            SequencePosition consumed;
                            SequencePosition examined;
                            while (_framer.TryReadMessageAsServer(ref buffer, out var message,
                                out consumed, out examined))
                            {
                                input.AdvanceTo(consumed, examined);
                                advanced = true;

                                try
                                {
                                    await _application.DispatchRequestAsync(message, connection);
                                }
                                catch (OperationCanceledException)
                                {
                                    // Don't treat OperationCanceledException as an error, it's basically a "control flow"
                                    // exception to stop things from running
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(EventIds.ApplicationError, ex, "Unhandled application exception: " + ex.Message);
                                    return;
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

                            break;
                        }
                    }
                    catch (InvalidDataException dataEx)
                    {
                        _logger.LogDebug(EventIds.ProtocolError, dataEx.Message);
                    }
                    catch (ConnectionResetException closeEx)
                    {
                        _logger.LogDebug(EventIds.ConnectionReset, closeEx.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(EventIds.UnknownConnectionError, ex.Message);
                    }
                    finally
                    {
                        await connection.Transport.Input.CompleteAsync();
                        await connection.Transport.Output.CompleteAsync();
                    }
                }
            }
            
        }
    }
}