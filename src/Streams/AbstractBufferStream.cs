namespace ExtenderApp.Buffer
{
    /// <summary> 对 AbstractBuffer<byte> 的 Stream 适配器，允许将 AbstractBuffer 用作 Stream 进行读写。 </summary>
    public sealed class AbstractBufferStream : Stream
    {
        private long position;
        private long length;
        private bool disposed;
        private AbstractBuffer<byte>? buffer;

        public override bool CanRead => !disposed && buffer != null;

        public override bool CanSeek => !disposed;

        public override bool CanWrite => !disposed;

        public override long Length
        {
            get
            {
                ThrowIfDisposed();
                return length;
            }
        }

        public override long Position
        {
            get
            {
                ThrowIfDisposed();
                return position;
            }
            set
            {
                ThrowIfDisposed();
                Seek(value, SeekOrigin.Begin);
            }
        }

        public AbstractBufferStream(AbstractBuffer<byte>? buffer) : this()
        {
            this.buffer = buffer;
        }

        public AbstractBufferStream()
        {
        }

        public override void Flush()
        {
            ThrowIfDisposed();
        }

        #region Read

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            ThrowIfDisposed();
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (this.buffer == null)
                throw new InvalidOperationException("当前流内没有可读取数据");
            if (position > this.buffer.Committed)
                return 0;

            var reader = this.buffer.GetReader();
            reader.Advance((int)position);

            int remaining = (int)Math.Min(reader.Remaining, buffer.Length);
            reader.TryRead(buffer.Slice(0, remaining));
            position += remaining;
            reader.Release();
            return remaining;
        }

        #endregion Read

        #region Write

        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ThrowIfDisposed();
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (this.buffer == null)
                this.buffer = MemoryBlock<byte>.GetBuffer((int)length);

            int count = buffer.Length;
            if (position + count < this.buffer.Committed)
            {
                this.buffer.UpdateCommitted(buffer, position);
            }
            else if (position == this.buffer.Committed)
            {
                this.buffer.Write(buffer);
            }
            else if (position < this.buffer.Committed && position + count > this.buffer.Committed)
            {
                int committedCount = (int)(this.buffer.Committed - position);
                this.buffer.UpdateCommitted(buffer.Slice(0, committedCount), position);
                this.buffer.Write(buffer.Slice(committedCount, count - committedCount));
            }
            else if (position > this.buffer.Committed && position < this.buffer.Capacity)
            {
                int diff = (int)(position - this.buffer.Committed);
                this.buffer.Advance(diff);
                this.buffer.Write(buffer);
            }
            else
            {
                int diff = (int)(position - this.buffer.Capacity);
                this.buffer.Advance(this.buffer.Available);
                this.buffer.GetSpan(diff + count);
                this.buffer.Advance(diff);
                this.buffer.Write(buffer);
            }

            position += count;
        }

        #endregion Write

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();

            long newPos;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPos = offset;
                    break;

                case SeekOrigin.Current:
                    newPos = position + offset;
                    break;

                case SeekOrigin.End:
                    newPos = Length + offset;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
            }

            if (newPos < 0)
                throw new IndexOutOfRangeException();

            position = newPos;
            return position;
        }

        public override void SetLength(long value)
        {
            length = value;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Try to release the buffer if possible
                    try
                    {
                        buffer?.TryRelease();
                    }
                    catch
                    {
                        // ignore
                    }
                }
                disposed = true;
            }
            base.Dispose(disposing);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(AbstractBufferStream));
        }
    }
}