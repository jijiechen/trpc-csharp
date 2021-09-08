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
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;

namespace TrpcSharp.Server.TrpcServices
{
    internal class TrpcServiceMethodBinder<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(ServiceAccessibility)]
#endif
        TService> : TrpcServiceBinderBase where TService : class
    {
#if NET5_0_OR_GREATER
        // Non-public methods is required by GetMethod overload that has a BindingFlags argument.
        internal const DynamicallyAccessedMemberTypes ServiceAccessibility = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
#endif

        private readonly TrpcServiceRouter _router;
        private readonly Type _declaringType;

        public TrpcServiceMethodBinder(TrpcServiceRouter router, Type declaringType)
        {
            _router = router;
            _declaringType = declaringType;
        }

        
        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, TrpcUnaryMethod<TRequest, TResponse> handler)
            where TRequest : class
            where TResponse: class
        {
            var (methodExecutor, metadata) = 
                CreateModelCore<TrpcUnaryMethod<TService, TRequest, TResponse>>(
                method.Name,
                new[] { typeof(TRequest), typeof(UnaryTrpcContext) });

            _router.AddUnaryMethod(method, metadata, methodExecutor);
        }
        
        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, TrpcClientStreamingMethod<TRequest> handler) 
            where TRequest : class
            where TResponse : class
        {
            var (methodExecutor, metadata) = 
                CreateModelCore<TrpcClientStreamingMethod<TService, TRequest>>(
                method.Name,
                new[] { typeof(TRequest), typeof(StreamTrpcContext) });

            _router.AddClientStreamingMethod(method, metadata, methodExecutor);
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, TrpcDuplexStreamingMethod<TRequest> handler) 
            where TRequest : class
            where TResponse : class
        {
            var (methodExecutor, metadata) =
                CreateModelCore<TrpcDuplexStreamingMethod<TService, TRequest>>(
                method.Name,
                new[] { typeof(TRequest), typeof(StreamTrpcContext) });

            _router.AddDuplexStreamingMethod(method, metadata, methodExecutor);
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, TrpcServerStreamingMethod<TRequest> handler)
            where TRequest : class
            where TResponse : class
        {
            var (methodExecutor, metadata) = 
                CreateModelCore<TrpcServerStreamingMethod<TService, TRequest>>(
                method.Name,
                new[] { typeof(TRequest), typeof(StreamTrpcContext) });

            _router.AddServerStreamingMethod(method, metadata, methodExecutor);
        }

        private (TDelegate methodExecutor, List<object> metadata) CreateModelCore<TDelegate>(string methodName, Type[] methodParameters) where TDelegate : Delegate
        {
            var handlerMethod = GetMethod(methodName, methodParameters);
            if (handlerMethod == null)
            {
                throw new InvalidOperationException($"Could not find '{methodName}' on {typeof(TService)}.");
            }

            var methodExecutor = (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), handlerMethod);

            var metadata = new List<object>();
            // Add type metadata first so it has a lower priority
            metadata.AddRange(typeof(TService).GetCustomAttributes(inherit: true));
            // Add method metadata last so it has a higher priority
            metadata.AddRange(handlerMethod.GetCustomAttributes(inherit: true));

            // Accepting CORS preflight means gRPC will allow requests with OPTIONS + preflight headers.
            // If CORS middleware hasn't been configured then the request will reach gRPC handler.
            // gRPC will return 405 response and log that CORS has not been configured.
            metadata.Add(new HttpMethodMetadata(new[] { "POST" }, acceptCorsPreflight: true));

            return (methodExecutor, metadata);
        }

        private MethodInfo GetMethod(string methodName, Type[] methodParameters)
        {
            var currentType = typeof(TService);
            while (currentType != null)
            {
                // Specify binding flags explicitly because we don't want to match static methods.
                var matchingMethod = currentType.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    types: methodParameters,
                    modifiers: null);

                if (matchingMethod == null)
                {
                    return null;
                }

                // Validate that the method overrides the virtual method on the base service type.
                // If there is a method with the same name it will hide the base method. Ignore it,
                // and continue searching on the base type.
                if (matchingMethod.IsVirtual)
                {
                    var baseDefinitionMethod = matchingMethod.GetBaseDefinition();
                    if (baseDefinitionMethod.DeclaringType == _declaringType)
                    {
                        return matchingMethod;
                    }
                }

                currentType = currentType.BaseType;
            }

            return null;
        }
    }
}