using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using TrpcSharp.Server.Extensions;

namespace TrpcSharp.Server
{
    public class TrpcDataStream: Stream
    {
        private readonly PipeReader _underlyingReader;
        private readonly long _totalLength;
        private long _unexaminedInputLength;
        private readonly TaskCompletionSource<bool> _accessedByApp;
        public TrpcDataStream(PipeReader underlyingReader, long length)
        {
            _underlyingReader = underlyingReader;
            _totalLength = length;
            _unexaminedInputLength = _totalLength;
            _accessedByApp = new TaskCompletionSource<bool>();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            return ReadAsyncWrapper(destination, cancellationToken);
        }


        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsyncWrapper(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }


        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return TaskToApm.Begin(ReadAsync(buffer, offset, count), callback, state);   
        }

        /// <inheritdoc />
        public override int EndRead(IAsyncResult asyncResult)
        {
            return TaskToApm.End<int>(asyncResult);
        }

        private ValueTask<int> ReadAsyncWrapper(Memory<byte> destination, CancellationToken cancellationToken)
        {
            try
            {
                return ReadAsyncInternal(destination, cancellationToken);
            }
            catch (ConnectionAbortedException ex)
            {
                throw new TaskCanceledException("The tRPC call was aborted", ex);
            }
            catch (ConnectionResetException ex)
            {
                throw new TaskCanceledException("The tRPC call was reset", ex);
            }
        }

        private async ValueTask<int> ReadAsyncInternal(Memory<byte> destination, CancellationToken cancellationToken)
        {
            while (_unexaminedInputLength > 0)
            {
                var result = await _underlyingReader.ReadAsync(cancellationToken);

                if (result.IsCanceled)
                {
                    throw new OperationCanceledException("The read was canceled");
                }

                if (result.IsCompleted)
                {
                    return 0;
                }

                var buffer = result.Buffer;
                var length = buffer.Length;
                var consumed = buffer.End;
                try
                {
                    if (length == 0)
                    {
                        return 0;
                    }
                    
                    var actual = (int)Math.Min(Math.Min(length, destination.Length), _unexaminedInputLength);

                    var slice = actual == length ? buffer : buffer.Slice(0, actual);
                    consumed = slice.End;
                    slice.CopyTo(destination.Span);

                    _unexaminedInputLength -= actual;
                    return actual;
                }
                finally
                {
                    _underlyingReader.AdvanceTo(consumed);
                }
            }
            
            return 0;
        }

        /// <inheritdoc />
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            while (_unexaminedInputLength > 0)
            {
                var result = await _underlyingReader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var buffer = result.Buffer;
                var position = buffer.Start;
                var consumed = position;

                try
                {
                    if (result.IsCanceled)
                    {
                        throw new OperationCanceledException("The read was canceled");
                    }

                    while (_unexaminedInputLength > 0 && buffer.TryGet(ref position, out ReadOnlyMemory<byte> memory, advance: false))
                    {
                        if (memory.Length > _unexaminedInputLength)
                        {
                            memory = memory[..(int)_unexaminedInputLength];
                        }
                        await destination.WriteAsync(memory, cancellationToken).ConfigureAwait(false);
                        
                        _unexaminedInputLength -= memory.Length;
                        position = buffer.GetPosition(memory.Length, position);
                        consumed = position;
                    }

                    // The while loop completed succesfully, so we've consumed the entire buffer.
                    consumed = buffer.End;

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
                finally
                {
                    // Advance even if WriteAsync throws so the PipeReader is not left in the
                    // currently reading state
                    _underlyingReader.AdvanceTo(consumed);
                }
            }
        }

        internal void MarkedAsAccessed()
        {
            _accessedByApp.TrySetResult(true);
        }

        /// <summary>
        /// 用于指示此 Stream 由业务应用获取访问的事件
        /// </summary>
        internal Task AccessByApp => _accessedByApp.Task;

        #region UnSupported

        public override long Length => _totalLength;
        
        public override void Flush()
        {
            
        }
        
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        

        #endregion
    }
}