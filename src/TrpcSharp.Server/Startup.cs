using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TrpcSharp.Protocol.Standard;
using TrpcSharp.Server.Trpc;

namespace TrpcSharp.Server
{
    public class Startup
    {
        public void Configure(ITrpcApplicationBuilder app)
        {
            app.Run(async ctx =>
            {
                Console.WriteLine($"New invocation: {ctx.Identifier}");

                if (ctx is UnaryTrpcContext unaryCtx)
                {
                    unaryCtx.UnaryResponse.ErrorMessage = $"Hello {ctx.Identifier}";
                }
                
                if (ctx is StreamTrpcContext streamCtx)
                {
                    await streamCtx.InitializeStreamingAsync(TrpcServerStreamingMode.DuplexStreaming);


                    var counter = 0;
                    while (counter++ < 10)
                    {
                        var hello = Encoding.UTF8.GetBytes("{\"distance\":" + counter + " }");
                        await streamCtx.SendChannel.Writer.WriteAsync(new MemoryStream(hello));
                        await Task.Delay(TimeSpan.FromMilliseconds(500));
                    }

                    streamCtx.SendChannel.Writer.TryComplete();
                    
                    await foreach (var stream in streamCtx.ReceiveChannel.Reader.ReadAllAsync())
                    {
                        var sr = new StreamReader(stream);
                        var json = await sr.ReadToEndAsync();
                        
                        Console.WriteLine($"Length {stream.Length} received: {json}");
                    }
                }

            });
        }
    }
}