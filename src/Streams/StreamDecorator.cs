namespace ExtenderApp.Buffer.Streams
{
    /// <summary>
    /// 一个用于 <see cref="Stream"/> 的装饰器，允许在运行时替换内部流。 所有操作都会转发到内部流，如果装饰器已被释放或内部流尚未设置，将抛出 <see cref="ObjectDisposedException"/> 或 <see cref="InvalidOperationException"/>。
    /// </summary>
    public class StreamDecorator : Stream
    {
        private readonly bool _leaveInnerStreamOpen;
        private bool disposed;
        private Stream? innerStream;

        /// <inheritdoc/>
        public override bool CanRead => EnsureStream().CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => EnsureStream().CanSeek;

        /// <inheritdoc/>
        public override bool CanWrite => EnsureStream().CanWrite;

        /// <inheritdoc/>
        public override long Length => EnsureStream().Length;

        /// <inheritdoc/>
        public override long Position
        {
            get => EnsureStream().Position;
            set => EnsureStream().Position = value;
        }

        public StreamDecorator() : this(true)
        {
        }

        public StreamDecorator(Stream stream, bool leaveInnerStreamOpen) : this(leaveInnerStreamOpen)
        {
            innerStream = stream;
        }

        public StreamDecorator(bool leaveInnerStreamOpen)
        {
            _leaveInnerStreamOpen = leaveInnerStreamOpen;
        }

        /// <summary>
        /// 设置或替换此装饰器使用的内部流。
        /// </summary>
        /// <param name="stream">要设置的流，不能为 <c>null</c>。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="stream"/> 为 <c>null</c> 时抛出。</exception>
        /// <exception cref="ObjectDisposedException">当此装饰器已被释放时抛出。</exception>
        public void SetInnerStream(Stream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));
            if (disposed)
                throw new ObjectDisposedException(nameof(StreamDecorator));
            this.innerStream = stream;
        }

        private Stream EnsureStream()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StreamDecorator));
            return innerStream ?? throw new InvalidOperationException("内部流尚未设置。");
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            EnsureStream().Flush();
        }

        /// <inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return EnsureStream().FlushAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return EnsureStream().Read(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override int Read(Span<byte> buffer)
        {
            return EnsureStream().Read(buffer);
        }

        /// <inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return EnsureStream().ReadAsync(buffer, offset, count, cancellationToken);
        }

        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return EnsureStream().ReadAsync(buffer, cancellationToken);
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            return EnsureStream().Seek(offset, origin);
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            EnsureStream().SetLength(value);
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureStream().Write(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureStream().Write(buffer);
        }

        /// <inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return EnsureStream().WriteAsync(buffer, offset, count, cancellationToken);
        }

        /// <inheritdoc/>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return EnsureStream().WriteAsync(buffer, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return EnsureStream().CopyToAsync(destination, bufferSize, cancellationToken);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing && !_leaveInnerStreamOpen)
                {
                    innerStream?.Dispose();
                }

                disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}