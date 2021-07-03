using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TrpcSharp.Protocol.Framing;

namespace TrpcSharp.Server.Trpc
{
    public static class TrpcServicesExtensions
    {
        public static IServiceCollection AddTrpcServer(this IServiceCollection services, IPEndPoint endPoint)
        {
            services.TryAddSingleton<ITrpcPacketFramer, DefaultTrpcPacketFramer>();
            services.TryAddSingleton<ITrpcApplicationBuilder, DefaultTrpcApplicationBuilder>();
            services.TryAddSingleton(sp =>
            {
                var builder = sp.GetService<ITrpcApplicationBuilder>();
                return builder!.Build();
            });
            services.TryAddSingleton<ITrpcApplication, TrpcApplication>();
            
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<KestrelServerOptions>, TrpcServerOptionsSetup>());

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
    }
}