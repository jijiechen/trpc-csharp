using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace TrpcSharp.Server.Trpc
{
    public interface IConnection
    {
        /// <summary>
        /// Gets or sets a unique identifier to represent this connection in trace logs.
        /// </summary>
        string ConnectionId { get; set; }
        /// <summary>
        /// Gets or sets the reader and writer to this connection
        /// </summary>
        IDuplexPipe Transport { get; set; }
        /// <summary>
        /// Aborts the underlying connection.
        /// </summary>
        Task AbortAsync();
    }

    public class AspNetCoreConnection : IConnection
    {
        private readonly ConnectionContext _connectionContext;

        public AspNetCoreConnection(ConnectionContext connectionContext)
        {
            _connectionContext = connectionContext;
        }

        public string ConnectionId
        {
            get => _connectionContext.ConnectionId;
            set => _connectionContext.ConnectionId = value;
        }

        public IDuplexPipe Transport
        {
            get => _connectionContext.Transport;
            set => _connectionContext.Transport = value;
        }
        
        public Task AbortAsync()
        {
            _connectionContext.Abort();
            return Task.CompletedTask;
        }
    }
}