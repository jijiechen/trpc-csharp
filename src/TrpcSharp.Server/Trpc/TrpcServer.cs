using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using TrpcSharp.Protocol;
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

            public TrpcConnectionHandler(ITrpcPacketFramer framer)
            {
                _framer = framer;
            }

            public override async Task OnConnectedAsync(ConnectionContext connection)
            {
                try
                {
                    while (!connection.ConnectionClosed.IsCancellationRequested)
                    {
                        var input = connection.Transport.Input;
                        var result = await input.ReadAsync();
                        var buffer = result.Buffer;

                        if (_framer.TryReadRequestMessage(ref buffer, out var message, out SequencePosition consumed, 
                            out SequencePosition examined))
                        {
                            ProcessMessage(message, connection.Transport.Output);
                        }
                        
                        if (result.IsCompleted)
                        {
                            break;
                        }

                        input.AdvanceTo(consumed, examined);
                    }
                }
                catch(ConnectionResetException closeEx)
                {
                    // todo: connection closed
                }
                finally
                {
                    // Complete the transport PipeReader and PipeWriter after calling into application code
                    await connection.Transport.Input.CompleteAsync();
                    await connection.Transport.Output.CompleteAsync();
                }
            }

            private void ProcessMessage(ITrpcRequestMessage trpcMessage, PipeWriter transportOutput)
            {
                switch (trpcMessage)
                {
                    case UnaryRequestMessage unaryMessage:
                        Console.WriteLine($"Unary request {unaryMessage.RequestId} has been well received.");
                        break;
                    case StreamRequestMessage streamMessage:
                        Console.WriteLine($"Stream message {streamMessage.StreamFrameType} {streamMessage.StreamId} has been well received.");
                        break;
                }
                
                
            }
            
            
            public void SendPacketAsync(PipeWriter transportOutput, ITrpcResponseMessage responseMessage)
            {
                var buffer = _framer.WriteResponseMessage(responseMessage);
                transportOutput.Write(buffer.Span);
            }
        }
    }
}