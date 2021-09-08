#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TrpcSharp.Server.TrpcServices
{
    internal sealed class DefaultTrpcServiceActivator : ITrpcServiceActivator
    {
        private static readonly Dictionary<Type, ObjectFactory> ObjectFactories = new();

        public TrpcServiceHandle Create(IServiceProvider serviceProvider, Type serviceType)
        {
            var service = serviceProvider.GetService(serviceType);
            if (service == null)
            {
                if (!ObjectFactories.TryGetValue(serviceType, out var factory))
                {
                    factory = ActivatorUtilities.CreateFactory(serviceType, Type.EmptyTypes);
                    ObjectFactories.TryAdd(serviceType, factory);
                }
                
                service = factory!.Invoke(serviceProvider, Array.Empty<object>());
                return new TrpcServiceHandle(service, created: true, state: null);
            }

            return new TrpcServiceHandle(service, created: false, state: null);
        }

        public ValueTask ReleaseAsync(TrpcServiceHandle service)
        {
            if (service.Instance == null)
            {
                throw new ArgumentException("Service instance is null.", nameof(service));
            }

            if (service.Created)
            {
                if (service.Instance is IAsyncDisposable asyncDisposableService)
                {
                    return asyncDisposableService.DisposeAsync();
                }

                if (service.Instance is IDisposable disposableService)
                {
                    disposableService.Dispose();
                    return default;
                }
            }

            return default;
        }
    }
}