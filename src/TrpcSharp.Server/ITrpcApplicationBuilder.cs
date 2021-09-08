using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TrpcSharp.Server.TrpcServices;
using BindingFlags = System.Reflection.BindingFlags;

namespace TrpcSharp.Server
{
    public interface ITrpcApplicationBuilder
    {
        void Use(Func<TrpcRequestDelegate, TrpcRequestDelegate> handler);
        void AddService(Type serviceType);
        TrpcRequestDelegate Build(IServiceProvider rootServiceProvider);
    }

    class DefaultTrpcApplicationBuilder : ITrpcApplicationBuilder
    {
        private readonly List<Func<TrpcRequestDelegate, TrpcRequestDelegate>> _components = new();
        private readonly List<Type> _serviceTypes = new();
        private readonly ILogger<DefaultTrpcApplicationBuilder> _logger;

        public DefaultTrpcApplicationBuilder(ILogger<DefaultTrpcApplicationBuilder> logger)
        {
            _logger = logger;
            this.Use<TrpcServiceMiddleware>();
        }
        
        public void Use(Func<TrpcRequestDelegate, TrpcRequestDelegate> handler)
        {
            _components.Add(handler);
        }

        public void AddService(Type serviceType)
        {
            _serviceTypes.Add(serviceType);
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
        
        public TrpcRequestDelegate Build(IServiceProvider rootServiceProvider)
        {
            TrpcRequestDelegate app = _ => Task.CompletedTask;
            for (var c = _components.Count - 1; c >= 0; c--)
            {
                app = _components[c](app);
            }

            BuildServices(rootServiceProvider);

            return app;
        }

        private void BuildServices(IServiceProvider rootServiceProvider)
        {
            var binderBaseType = typeof(TrpcServiceMethodBinder<>);
            var router = (TrpcServiceRouter)rootServiceProvider.GetService(typeof(TrpcServiceRouter));
            foreach (var serviceType in _serviceTypes)
            {
                var bindMethodInfo = BindTrpcServiceMethodFinder.GetBindMethod(serviceType);
                // Invoke BindService(ServiceBinderBase, BaseType)
                if (bindMethodInfo == null)
                {
                    _logger.LogDebug($"No 'BindTrpcService' method found in type '{serviceType.FullName}'");
                    continue;
                }

                // The second parameter is always the service base type
                var serviceParameter = bindMethodInfo.GetParameters()[1];

                var binderType = binderBaseType.MakeGenericType(new[] { serviceType });
                var ctor = binderType.GetConstructor(new[] { typeof(TrpcServiceRouter), typeof(Type) });
                var binder = ctor!.Invoke(new object[] { router, serviceParameter.ParameterType });

                try
                {
                    bindMethodInfo.Invoke(null, new[] { binder, null });
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error binding tRPC service '{serviceType.FullName}'", ex);
                }
            }
        }
    }
    

}