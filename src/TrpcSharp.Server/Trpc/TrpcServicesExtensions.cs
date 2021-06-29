using System.Net;
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
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<KestrelServerOptions>, TrpcServerOptionsSetup>());

            services.Configure<ServerOptions>(o =>
            {
                o.EndPoint = endPoint;
            });

            services.TryAddSingleton<ITrpcPacketFramer, DefaultTrpcPacketFramer>();
            return services;
        }
    }
}