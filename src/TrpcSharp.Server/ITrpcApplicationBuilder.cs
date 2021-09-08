using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TrpcSharp.Server
{
    public interface ITrpcApplicationBuilder
    {
        void Use(ITrpcMiddleware middleware);
        void Use(Func<TrpcRequestDelegate, TrpcRequestDelegate> handler);
        TrpcRequestDelegate Build();
    }

    class DefaultTrpcApplicationBuilder : ITrpcApplicationBuilder
    {
        private readonly List<Func<TrpcRequestDelegate, TrpcRequestDelegate>> _components = new();

        
        public void Use(ITrpcMiddleware middleware)
        {
            _components.Add(WrapMiddleware(middleware));
        }

        public void Use(Func<TrpcRequestDelegate, TrpcRequestDelegate> handler)
        {
            _components.Add(handler);
        }

        private Func<TrpcRequestDelegate, TrpcRequestDelegate> WrapMiddleware(ITrpcMiddleware middleware)
        {
            return next =>
            {
                return async ctx =>
                {
                    await middleware.Invoke(ctx, next);
                };
            };
        }
        
        
        public TrpcRequestDelegate Build()
        {
            TrpcRequestDelegate app = _ => Task.CompletedTask;
            for (var c = _components.Count - 1; c >= 0; c--)
            {
                app = _components[c](app);
            }
            return app;
        }
    }
    
    public static class AppBuilderExtensions
    {   public static void Use<TMiddleware>(this ITrpcApplicationBuilder app) where TMiddleware: ITrpcMiddleware
        {   
            app.Use((next) =>
            {
                return async (ctx) =>
                {
                    var middleware = (TMiddleware) ctx.Services.GetService(typeof(TMiddleware));
                    await middleware.Invoke(ctx, next);
                };
            });
        }
    }
}