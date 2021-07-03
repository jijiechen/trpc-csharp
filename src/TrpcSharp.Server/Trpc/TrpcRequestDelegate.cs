using System.Threading.Tasks;

namespace TrpcSharp.Server.Trpc
{
    /// <summary>
    /// 表示一个可以处理 tRPC 调用的功能
    /// </summary>
    /// <param name="context">The <see cref="TrpcContext"/> for the request.</param>
    /// <returns>A task that represents the completion of request processing.</returns>   
    public delegate Task TrpcRequestDelegate(TrpcContext context);
    
    public interface ITrpcMiddleware
    {
        Task Invoke(TrpcContext trpcContext, TrpcRequestDelegate next);
    }
}