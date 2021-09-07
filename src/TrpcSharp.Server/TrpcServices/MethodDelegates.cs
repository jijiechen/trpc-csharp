using System.Threading.Tasks;

namespace TrpcSharp.Server.TrpcServices
{
    /// <summary>Server-side handler for unary call.</summary>
    public delegate Task<TResponse> TrpcUnaryMethod<in TRequest, TResponse>(TRequest request, UnaryTrpcContext context)
        where TRequest : class
        where TResponse : class;
    
    /// <summary>Server-side handler for client streaming call.</summary>
    public delegate Task TrpcClientStreamingMethod<in TRequest>(TRequest request, StreamTrpcContext context) where TRequest : class;
    
    /// <summary>Server-side handler for bidi streaming call.</summary>
    public delegate Task TrpcDuplexStreamingMethod<in TRequest>(TRequest request, StreamTrpcContext context) where TRequest : class;
    
    /// <summary>Server-side handler for server streaming call.</summary>
    public delegate Task TrpcServerStreamingMethod<in TRequest>(TRequest request, StreamTrpcContext context) where TRequest : class;
    
    
    /// <summary>Server-side handler for unary call.</summary>
    public delegate Task<TResponse> TrpcUnaryMethod<TService, in TRequest, TResponse>(TService serviceInstance, TRequest request, UnaryTrpcContext context)
        where TRequest : class
        where TResponse : class;
    
    /// <summary>Server-side handler for client streaming call.</summary>
    public delegate Task TrpcClientStreamingMethod<TService, in TRequest>(TService serviceInstance, TRequest request, StreamTrpcContext context) where TRequest : class;
    
    /// <summary>Server-side handler for bidi streaming call.</summary>
    public delegate Task TrpcDuplexStreamingMethod<TService, in TRequest>(TService serviceInstance, TRequest request, StreamTrpcContext context) where TRequest : class;
    
    /// <summary>Server-side handler for server streaming call.</summary>
    public delegate Task TrpcServerStreamingMethod<TService, in TRequest>(TService serviceInstance, TRequest request, StreamTrpcContext context) where TRequest : class;
}