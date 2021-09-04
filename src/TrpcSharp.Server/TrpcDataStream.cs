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

        public TrpcDataStream(PipeReader underlyingReader, long length)
        {
            _underlyingReader = underlyingReader;
            _totalLength = length;
            _unexaminedInputLength = _totalLength;
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
            while (true)
            {
                var result = await _underlyingReader.ReadAsync(cancellationToken);

                if (result.IsCanceled)
                {
                    throw new OperationCanceledException("The read was canceled");
                }

                var buffer = result.Buffer;
                var length = buffer.Length;

                var consumed = buffer.End;
                try
                {
                    if (length != 0)
                    {
                        var maxLength = _totalLength - _unexaminedInputLength;
                        var actual = (int)Math.Min(Math.Min(length, destination.Length), maxLength);

                        var slice = actual == length ? buffer : buffer.Slice(0, actual);
                        consumed = slice.End;
                        slice.CopyTo(destination.Span);

                        _unexaminedInputLength -= actual;
                        return actual;
                    }

                    if (_unexaminedInputLength == 0)
                    {
                        return 0;
                    }

                    if (result.IsCompleted)
                    {
                        return 0;
                    }
                }
                finally
                {
                    _underlyingReader.AdvanceTo(consumed);
                }
            }
         
        }

        /// <inheritdoc />
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            return _underlyingReader.CopyToAsync(destination, cancellationToken);
        }



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