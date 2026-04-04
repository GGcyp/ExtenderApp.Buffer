namespace ExtenderApp.Buffer.Streams
{
    /// <summary>
    /// 基于缓冲区的 <see cref="Stream"/> 装饰器。 用于将内部的读取/写入缓冲区暴露为 <see cref="Stream"/> 接口，便于在不直接操作缓冲区的情况下进行数据读写。
    /// </summary>
    public sealed class AbstractBufferStreamDecorator : Stream
    {
        private bool disposed;
        private AbstractBuffer<byte>? readBuffer;
        private AbstractBufferReader<byte>? readBufferReader;
        private MemoryBlock<byte>? writeBuffer;

        /// <summary>
        /// 指示该流是否支持读取。此实现始终返回 <c>true</c>，表示可从已设置的读取缓冲区读取数据。
        /// </summary>
        public override bool CanRead => true;

        /// <summary>
        /// 指示该流是否支持寻址。此实现不支持寻址，始终返回 <c>false</c>。
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// 指示该流是否支持写入。此实现始终返回 <c>true</c>，表示可以向内部写入缓冲区写入数据。
        /// </summary>
        public override bool CanWrite => true;

        /// <summary>
        /// 获取流的长度。此实现返回当前读取缓冲区中剩余可读字节数，如果未设置读取缓冲区则返回 0。
        /// </summary>
        public override long Length => readBufferReader?.Remaining ?? 0;

        /// <summary>
        /// 获取或设置流位置。此流不支持获取或设置位置，调用将抛出 <see cref="NotImplementedException"/>。
        /// </summary>
        /// <exception cref="NotImplementedException">始终抛出。</exception>
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// 刷新流。此实现为空操作，因为写入直接写入内部内存块，不需要额外刷盘操作。
        /// </summary>
        public override void Flush()
        {
        }

        /// <summary>
        /// 设置用于读取的缓冲区。会尝试释放之前设置的读取缓冲区并获取新的读取器。
        /// </summary>
        /// <param name="buffer">要设置的读取缓冲区，不能为空。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="buffer"/> 为 <c>null</c> 时抛出。</exception>
        public void SetReadBuffer(AbstractBuffer<byte> buffer)
        {
            readBuffer?.TryRelease();
            readBufferReader?.Release();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            readBuffer = buffer;
            readBufferReader = buffer.GetReader();
        }

        /// <summary>
        /// 获取并清空当前写入缓冲区的内存块引用。调用方负责对返回的 <see cref="MemoryBlock{Byte}"/> 进行处理或释放。
        /// </summary>
        /// <returns>当前写入缓冲区的 <see cref="MemoryBlock{Byte}"/>，如果没有则返回 <c>null</c>。</returns>
        public MemoryBlock<byte>? GetWriteBuffer()
        {
            var buffer = writeBuffer;
            writeBuffer = null;
            return buffer;
        }

        public void Clear()
        {
            writeBuffer?.TryRelease();
            writeBuffer = null;
            readBuffer?.TryRelease();
            readBuffer = null;
            readBufferReader?.Release();
            readBufferReader = null;
        }

        #region Read

        /// <summary>
        /// 从内部读取缓冲区中读取数据并写入到目标字节数组中。
        /// </summary>
        /// <param name="buffer">目标字节数组。</param>
        /// <param name="offset">写入目标数组的起始偏移。</param>
        /// <param name="count">最多读取的字节数。</param>
        /// <returns>实际读取的字节数。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="buffer"/> 为 <c>null</c> 时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="offset"/> 或 <paramref name="count"/> 越界时抛出。</exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            return Read(buffer.AsSpan(offset, count));
        }

        /// <summary>
        /// 从内部读取缓冲区中读取数据并写入到目标 <see cref="Span{Byte}"/> 中。
        /// </summary>
        /// <param name="buffer">目标缓冲区。</param>
        /// <returns>实际读取的字节数。</returns>
        /// <exception cref="ArgumentNullException">当未设置读取缓冲区或 <paramref name="buffer"/> 为空时抛出。</exception>
        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty || readBufferReader == null)
                throw new ArgumentNullException("还未设置读取的缓冲区");

            return readBufferReader.Read(buffer);
        }

        /// <summary>
        /// 异步读取数据到字节数组中（基于同步实现，立即返回已完成的任务）。
        /// </summary>
        /// <param name="buffer">目标数组。</param>
        /// <param name="offset">写入目标数组的起始偏移。</param>
        /// <param name="count">最多读取字节数。</param>
        /// <param name="cancellationToken">取消令牌（当前未使用）。</param>
        /// <returns>表示读取操作的已完成任务，结果为实际读取的字节数。</returns>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            if (readBufferReader == null)
                return Task.FromException<int>(new ArgumentNullException(nameof(buffer)));

            return Task.FromResult(readBufferReader.Read(buffer.AsSpan(offset, count)));
        }

        /// <summary>
        /// 异步读取数据到 <see cref="Memory{Byte}"/>（基于同步实现，立即返回已完成的结果）。
        /// </summary>
        /// <param name="buffer">目标缓冲区。</param>
        /// <param name="cancellationToken">取消令牌（当前未使用）。</param>
        /// <returns>实际读取的字节数。</returns>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty || readBufferReader == null)
                return ValueTask.FromException<int>(new ArgumentNullException("还未设置读取的缓冲区"));

            return ValueTask.FromResult(readBufferReader.Read(buffer));
        }

        #endregion Read

        #region Write

        /// <summary>
        /// 将字节数组中的数据写入到内部写缓冲区。
        /// </summary>
        /// <param name="buffer">源字节数组。</param>
        /// <param name="offset">源数组起始偏移。</param>
        /// <param name="count">要写入的字节数。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="buffer"/> 为 <c>null</c> 时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="offset"/> 或 <paramref name="count"/> 越界时抛出。</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            Write(buffer.AsSpan(offset, count));
        }

        /// <summary>
        /// 将 <see cref="ReadOnlySpan{Byte}"/> 中的数据写入到内部写缓冲区。
        /// </summary>
        /// <param name="buffer">要写入的数据。</param>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty)
                return;
            if (writeBuffer == null)
                writeBuffer = MemoryBlock<byte>.GetBuffer(buffer.Length);

            buffer.CopyTo(writeBuffer.GetSpan(buffer.Length));
            writeBuffer.Advance(buffer.Length);
        }

        /// <summary>
        /// 异步方式将字节数组写入到内部写缓冲区（基于同步实现，立即返回已完成的任务）。
        /// </summary>
        /// <param name="buffer">源数组。</param>
        /// <param name="offset">起始偏移。</param>
        /// <param name="count">写入字节数。</param>
        /// <param name="cancellationToken">取消令牌（当前未使用）。</param>
        /// <returns>已完成的任务。</returns>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            if (writeBuffer == null)
                writeBuffer = MemoryBlock<byte>.GetBuffer(buffer.Length);

            buffer.CopyTo(writeBuffer.GetSpan(buffer.Length));
            writeBuffer.Advance(buffer.Length);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 异步方式将 <see cref="ReadOnlyMemory{Byte}"/> 写入到内部写缓冲区（基于同步实现，立即返回已完成的结果）。
        /// </summary>
        /// <param name="buffer">要写入的数据。</param>
        /// <param name="cancellationToken">取消令牌（当前未使用）。</param>
        /// <returns>已完成的 <see cref="ValueTask"/>。</returns>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty)
                return ValueTask.CompletedTask;
            if (writeBuffer == null)
                writeBuffer = MemoryBlock<byte>.GetBuffer(buffer.Length);

            buffer.CopyTo(writeBuffer.GetMemory(buffer.Length));
            writeBuffer.Advance(buffer.Length);
            return ValueTask.CompletedTask;
        }

        #endregion Write

        /// <summary>
        /// 不支持在此流上执行寻址操作，调用将抛出 <see cref="NotImplementedException"/>。
        /// </summary>
        /// <exception cref="NotImplementedException">始终抛出。</exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 不支持设置此流的长度，调用将抛出 <see cref="NotImplementedException"/>。
        /// </summary>
        /// <exception cref="NotImplementedException">始终抛出。</exception>
        public override void SetLength(long value)
        {
            if (writeBuffer == null)
                writeBuffer = MemoryBlock<byte>.GetBuffer((int)value);
            else if (value > writeBuffer.Capacity)
                writeBuffer.GetSpan((int)(writeBuffer.Capacity - value));
        }

        /// <summary>
        /// 释放流并尝试释放内部的读/写缓冲区资源。若释放过程中发生异常则会被吞掉以保证释放的稳健性。
        /// </summary>
        /// <param name="disposing">指示是否来自托管代码的释放调用。</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // 尝试释放内部缓冲区资源
                    try
                    {
                        readBuffer?.TryRelease();
                        writeBuffer?.TryRelease();
                        readBufferReader?.Release();
                    }
                    catch
                    {
                        // 忽略释放期间的异常
                    }
                }
                disposed = true;
            }
            base.Dispose(disposing);
        }
    }
}