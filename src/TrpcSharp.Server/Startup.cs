using System;
using TrpcSharp.Server.Trpc;

namespace TrpcSharp.Server
{
    public class Startup
    {
        public void Configure(ITrpcApplicationBuilder app)
        {
            app.Run(async ctx =>
            {
                var unaryCtx = (ctx as UnaryTrpcContext);
                unaryCtx.UnaryResponse.ErrorMessage = $"Hello {ctx.Id}";
                // , callee: {unaryCtx.UnaryRequest.Callee}
                Console.WriteLine($"New invocation: {ctx.Id}");
                
                // ctx.UnaryResponse
            });
        }
    }
}