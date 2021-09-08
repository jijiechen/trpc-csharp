using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TrpcSharp.Protocol.Framing;
using TrpcSharp.Server.TrpcServices;

namespace TrpcSharp.Server
{
    public static class TrpcServicesExtensions
    {
        public static IServiceCollection AddTrpcServer(this IServiceCollection services, IPEndPoint endPoint)
        {
            services.TryAddSingleton<ITrpcPacketFramer, DefaultTrpcPacketFramer>();
            services.TryAddSingleton<ITrpcApplicationBuilder, DefaultTrpcApplicationBuilder>();
            services.TryAddSingleton<ITrpcMessageDispatcher, TrpcMessageDispatcher>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TrpcMessageDispatcher>(
                sp => (TrpcMessageDispatcher)(sp.GetService<ITrpcMessageDispatcher>()) ));
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<KestrelServerOptions>, TrpcServerOptionsSetup>());
            
            services.TryAddSingleton<ITrpcServiceActivator, DefaultTrpcServiceActivator>();
            services.TryAddSingleton<TrpcServiceRouter>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ITrpcMiddleware, TrpcServiceMiddleware>());
            services.Configure<ServerOptions>(o =>
            {
                o.EndPoint = endPoint;
            });
            return services;
        }
        
        public static ITrpcApplicationBuilder Run(this ITrpcApplicationBuilder app, TrpcRequestDelegate requestHandler)
        {
            app.Use(_ => requestHandler);
            return app;
        }
        
        public static ITrpcApplicationBuilder AddService<TService>(this ITrpcApplicationBuilder app) where TService: class
        {
           // TrpcServiceMethodBinder add service
           // TrpcServiceMethodBinder add method
            return app;
        }
    }
}