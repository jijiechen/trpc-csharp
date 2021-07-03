using System;
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
                Console.WriteLine($"New invocation: {ctx.Id}");

                if (ctx is UnaryTrpcContext unaryCtx)
                {
                    unaryCtx.UnaryResponse.ErrorMessage = $"Hello {ctx.Id}";
                }
                
                if (ctx is StreamTrpcContext streamCtx && streamCtx.StreamMessage.StreamFrameType == TrpcStreamFrameType.TrpcStreamFrameData)
                {
                    // await streamCtx.Push()
                }
                
                
            });
        }
    }
}