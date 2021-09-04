using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TrpcSharp.Server.Extensions
{
    internal static class ServiceProviderServiceExtensions
    {
        public static AsyncServiceScope CreateAsyncScope(this IServiceScopeFactory scopeFactory)
        {
            return new AsyncServiceScope(scopeFactory.CreateScope());
        }
    }

    internal class AsyncServiceScope : IServiceScope, IAsyncDisposable
    {
        private readonly IServiceScope _serviceScope;

        public AsyncServiceScope(IServiceScope serviceScope)
        {
            _serviceScope = serviceScope;
        }

        public IServiceProvider ServiceProvider => _serviceScope.ServiceProvider;

        public void Dispose()
        {
            _serviceScope.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            if (_serviceScope is IAsyncDisposable ad)
            {
                return ad.DisposeAsync();
            }
            _serviceScope.Dispose();
            return new ValueTask();
        }
    }
}