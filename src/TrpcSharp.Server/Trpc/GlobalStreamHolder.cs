using System;
using System.Collections.Concurrent;
using TrpcSharp.Protocol.Standard;

namespace TrpcSharp.Server.Trpc
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
            if (!_allStreams.TryRemove(connectionId, out var connStreams))
            {
                return false;
            }
            
            return connStreams.TryGetValue(streamId, out streamTrpcContext);
        }
        
        public bool TryRemoveConnection(string connectionId)
        {
            if (!_allStreams.TryRemove(connectionId, out var connStreams))
            {
                return false;
            }

            foreach (var streamCtx in connStreams.Values)
            {
                streamCtx.StreamMessage = null;
                streamCtx.Services = null;
                streamCtx.Connection = null;
            }
            connStreams.Clear();
            return true;
        }
        
        public bool TryRemoveStream(string connectionId, uint streamId)
        {
            if (!_allStreams.TryRemove(connectionId, out var connStreams))
            {
                return false;
            }
            
            if (!connStreams.TryRemove(streamId, out var streamCtx))
            {
                return false;
            }

            streamCtx.StreamMessage = null;
            streamCtx.Services = null;
            streamCtx.Connection = null;
            return true;
        }

    }
}