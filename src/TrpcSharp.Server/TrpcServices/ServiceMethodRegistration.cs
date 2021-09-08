using System;
using System.Collections.Generic;
using Grpc.Core;
using TrpcSharp.Server.TrpcServices.ServiceMethodCallers;

namespace TrpcSharp.Server.TrpcServices
{
    internal class ServiceMethodRegistration
    {
        public IMethod Method { get; set; }
        public List<object> Metadata { get; set; }
        
        public ITrpcServiceMethodCaller Caller { get; set; }
    }
}