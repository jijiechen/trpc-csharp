using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Server
{
    internal class GlobalStreamHolder
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<uint, StreamTrpcContext>> _allStreams = new();

        public void AddStream(TrpcContext initContext)
        {
            if (initContext is StreamTrpcContext streamCtx &&
                streamCtx.StreamMessage.StreamFrameType == TrpcStreamFrameType.TrpcStreamFrameInit)
            {
                var connId = initContext.Connection.ConnectionId;
                if (!_allStreams.TryRemove(connId, out var connStreams))
                {
                    connStreams = new ConcurrentDictionary<uint, StreamTrpcContext>();
                    _allStreams.TryAdd(connId, connStreams);
                }

                connStreams.TryAdd(streamCtx.Identifier.Id, streamCtx);
            }
            else
            {
                throw new InvalidOperationException("Stream should be initialized by a tRPC message of frame type TrpcStreamFrameInit.");
            }
        }

        public bool TryGetStream(string connectionId, uint streamId, out StreamTrpcContext streamTrpcContext)
        {
            streamTrpcContext = null;
            if (!_allStreams.TryGetValue(connectionId, out var connStreams))
            {
                return false;
            }
            
            return connStreams.TryGetValue(streamId, out streamTrpcContext);
        }
        
        public async Task<bool> TryRemoveConnection(string connectionId)
        {
            if (!_allStreams.TryRemove(connectionId, out var connStreams))
            {
                return false;
            }

            foreach (var streamCtx in connStreams.Values)
            {
                await CleanupStream(streamCtx, TrpcStreamCloseType.TrpcStreamClose);
            }
            connStreams.Clear();
            return true;
        }
        
        public async Task<bool> TryRemoveStream(string connectionId, uint streamId, TrpcStreamCloseType closeType)
        {
            if (!_allStreams.TryRemove(connectionId, out var connStreams))
            {
                return false;
            }

            if (!connStreams.TryGetValue(streamId, out var streamCtx))
            {
                return false;
            }
            
            await CleanupStream(streamCtx, closeType);
            connStreams.TryRemove(streamId, out _);       
            return true;
        }

        private static async Task CleanupStream(StreamTrpcContext streamCtx, TrpcStreamCloseType closeType)
        {
            var tracker = (streamCtx as IStreamCallTracker);
            await tracker.CompleteAsync(streamCtx.Identifier.Id, closeType);
            
            streamCtx.ReceiveChannel?.Writer.TryComplete();
            streamCtx.SendChannel?.Writer.TryComplete();

            streamCtx.StreamMessage = null;
            streamCtx.Services = null;
            streamCtx.Connection = null;
        }
    }
}