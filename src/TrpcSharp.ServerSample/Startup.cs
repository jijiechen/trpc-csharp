using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TrpcSharp.Server;

namespace TrpcSharp.ServerSample
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
                    Console.WriteLine($"Func: {unaryCtx.Request.Func}");
                    if (unaryCtx.Request.Data != null)
                    {
                        var content = await new StreamReader(unaryCtx.Request.Data).ReadToEndAsync();
                        Console.WriteLine($"Request Content: {content}");
                    }
                    unaryCtx.Response.ErrorMessage = $"Hello {ctx.Identifier}";
                }
                
                if (ctx is StreamTrpcContext streamCtx)
                {
                    await streamCtx.InitializeStreamingAsync(TrpcServerStreamingMode.DuplexStreaming);


                    var counter = 0;
                    while (counter++ < 10)
                    {
                        var hello = Encoding.UTF8.GetBytes("{\"distance\":" + counter + " }");
                        await streamCtx.WriteAsync(new MemoryStream(hello));
                        await Task.Delay(TimeSpan.FromMilliseconds(500));
                    }
                    
                    await foreach (var stream in streamCtx.ReadAllAsync())
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