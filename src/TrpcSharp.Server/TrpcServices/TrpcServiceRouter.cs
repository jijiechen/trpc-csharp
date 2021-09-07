using System;
using System.Collections.Generic;

namespace TrpcSharp.Server.TrpcServices
{
    public class TrpcServiceRouter
    {
        private readonly Dictionary<string, Type> _services = new();

        public void Register(string service, Type serviceType)
        {
            if (string.IsNullOrEmpty(service))
            {
                throw new ArgumentException("A service must have a name");
            }
            
            _services[service] = serviceType;
        }
        
        public Type Route(string service)
        {
            return _services.TryGetValue(service, out var type) ? type : null;
        }
    }
}