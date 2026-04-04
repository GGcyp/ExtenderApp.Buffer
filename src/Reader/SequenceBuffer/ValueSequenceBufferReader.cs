using System.Buffers;
using ExtenderApp.Buffer.Reader;
using ExtenderApp.Buffer.SequenceBuffers;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 针对 <see cref="SequenceBuffer{T}"/> 的向前只读读取器（轻量结构）。 在构造时会冻结目标缓冲以防止被释放；使用结束后应调用 <see cref="Dispose"/> 以尝试释放/解冻缓冲。
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    public struct ValueSequenceBufferReader<T> : IDisposable, IEquatable<ValueSequenceBufferReader<T>>
    {
        private SequenceBufferSegment<T>? currentSegment;
        private int segmentConsumed; // 已在当前段内消费的数量（相对于段起点）
        public SequenceBuffer<T> Buffer;

        /// <summary>
        /// 已消费（已读取）的总元素数（相对于序列起点）。
        /// </summary>
        public long Consumed { get; private set; }

        /// <summary>
        /// 构造一个读取器以读取指定序列，初始读取位置为序列起点（0）。
        /// </summary>
        /// <param name="buffer">要读取的 <see cref="SequenceBuffer{T}"/>，不能为空。</param>
        public ValueSequenceBufferReader(SequenceBuffer<T> buffer)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Buffer.Freeze();
            currentSegment = buffer.First;
            segmentConsumed = 0;
            Consumed = 0;
        }

        /// <summary>
        /// 剩余可读的元素数量（等于序列的已提交长度减去当前已消费数）。
        /// </summary>
        public long Remaining => Buffer.Committed - Consumed;

        /// <summary>
        /// 指示是否已读尽序列的已提交数据。
        /// </summary>
        public bool IsCompleted => Remaining == 0;

        public ReadOnlySequence<T> UnreadSequence
        {
            get
            {
                if (currentSegment == null)
                    return ReadOnlySequence<T>.Empty;

                return new ReadOnlySequence<T>(currentSegment, segmentConsumed, Buffer.Last ?? SequenceBufferSegment<T>.Empty, (int)(Buffer.Last?.Committed ?? 0));
            }
        }

        /// <summary>
        /// 尝试预览下一个元素而不推进读取位置。
        /// </summary>
        /// <param name="item">输出的元素（当返回 true 时有效）。</param>
        /// <returns>若存在下一个元素则返回 true，否则返回 false。</returns>
        public bool TryPeek(out T item)
        {
            if (Remaining <= 0)
            {
                item = default!;
                return false;
            }

            item = UnreadSequence.FirstSpan[0];
            return true;
        }

        /// <summary>
        /// 尝试读取下一个元素并推进位置。
        /// </summary>
        /// <param name="item">读取到的元素（当返回 true 时有效）。</param>
        /// <returns>如成功读取返回 true，否则返回 false（例如已无数据）。</returns>
        public bool TryRead(out T item)
        {
            if (!TryPeek(out item))
                return false;

            Advance(1);
            return true;
        }

        /// <summary>
        /// 尝试读取并复制指定长度到目标跨度，如果目标长度大于剩余则返回 false，不推进位置。
        /// </summary>
        /// <param name="destination">目标跨度，用于接收数据。</param>
        /// <returns>当成功复制并推进位置时返回 true；若剩余不足返回 false 且不改变状态。</returns>
        public bool TryRead(Span<T> destination)
        {
            if (destination.Length == 0)
                return true;
            if (Remaining < destination.Length)
                return false;

            UnreadSequence.CopyTo(destination);
            Advance(destination.Length);
            return true;
        }

        public bool TryRead(int count, out ReadOnlySequence<T> sequence)
        {
            if (count < 0 || Remaining < count)
            {
                sequence = default;
                return false;
            }

            sequence = UnreadSequence.Slice(0, count);
            Advance(count);
            return true;
        }

        /// <summary>
        /// 读取下一个元素并推进位置，若无数据则抛出异常。
        /// </summary>
        /// <returns>读取到的元素。</returns>
        /// <exception cref="InvalidOperationException">当没有更多数据可读时抛出。</exception>
        public T Read()
        {
            if (!TryRead(out T item))
                throw new InvalidOperationException("没有可读数据。");
            return item;
        }

        /// <summary>
        /// 将尽可能多的数据复制到目标跨度并推进相应的读取位置，返回实际复制的元素数量。
        /// </summary>
        /// <param name="destination">目标跨度。</param>
        /// <returns>实际复制并消费的元素数量（可能小于目标长度）。</returns>
        public int Read(Span<T> destination)
        {
            if (destination.Length == 0)
                return 0;

            int toRead = (int)Math.Min(Remaining, destination.Length);
            UnreadSequence.CopyTo(destination);
            Advance(toRead);
            return toRead;
        }

        /// <summary>
        /// 将尽可能多的数据复制到目标内存并推进读取位置，返回实际复制的元素数量。
        /// </summary>
        public int Read(Memory<T> destination) => Read(destination.Span);

        /// <summary>
        /// 将尽可能多的数据写入目标缓冲并推进双方的位置，返回实际复制的元素数量。
        /// </summary>
        public int Read(AbstractBuffer<T> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int toCopy = (int)Math.Min(Remaining, destination.Available);
            if (toCopy == 0) return 0;

            int remaining = toCopy;
            // 获取目标内存并写入分片
            while (remaining > 0)
            {
                int dstRequested = remaining;
                var destMem = destination.GetMemory(dstRequested);
                var destSpan = destMem.Span;
                int written = Read(destSpan.Slice(0, Math.Min(destSpan.Length, remaining)));
                destination.Advance(written);
                remaining -= written;
            }

            return toCopy;
        }

        /// <summary>
        /// 将当前读取位置向前推进指定数量的元素（可以跨段推进）。
        /// </summary>
        /// <param name="count">推进的元素数量（必须为非负且不超过剩余）。</param>
        public void Advance(int count)
        {
            if (count < 0 || Consumed + count > Buffer.Committed)
                throw new ArgumentOutOfRangeException(nameof(count));

            int remaining = count;
            while (remaining > 0)
            {
                if (currentSegment == null) throw new InvalidOperationException("序列段异常。");
                int segCommitted = (int)currentSegment.Committed;
                int avail = segCommitted - segmentConsumed;
                if (avail == 0)
                {
                    // 到下一个段
                    if (!MoveToNextNonEmptySegment()) throw new InvalidOperationException("尝试推进时序列段耗尽。");
                    continue;
                }

                int step = Math.Min(avail, remaining);
                segmentConsumed += step;
                Consumed += step;
                remaining -= step;

                if (segmentConsumed >= segCommitted)
                {
                    // 跳到下一段
                    MoveToNextNonEmptySegment();
                }
            }
        }

        /// <summary>
        /// 将读取位置回退指定数量的元素（可以跨段回退）。
        /// </summary>
        /// <param name="count">回退的元素数量（必须为非负且不超过当前已消费）。</param>
        public void Rewind(int count)
        {
            if (count < 0 || count > Consumed)
                throw new ArgumentOutOfRangeException(nameof(count));

            int remaining = count;
            while (remaining > 0)
            {
                if (currentSegment == null)
                {
                    // 若当前为 null，定位到最后段
                    currentSegment = Buffer.First;
                    segmentConsumed = (int)(currentSegment?.Committed ?? 0);
                }

                if (segmentConsumed > 0)
                {
                    int step = Math.Min(segmentConsumed, remaining);
                    segmentConsumed -= step;
                    Consumed -= step;
                    remaining -= step;
                }
                else
                {
                    // 移到前一段
                    currentSegment = currentSegment.Prev;
                    if (currentSegment != null)
                        segmentConsumed = (int)currentSegment.Committed;
                    else
                        segmentConsumed = 0;
                }
            }
        }

        /// <summary>
        /// 重置读取器（已消费位置设为 0，回到序列起点）。
        /// </summary>
        public void Reset()
        {
            Consumed = 0;
            currentSegment = Buffer.First;
            segmentConsumed = 0;
        }

        private bool MoveToNextNonEmptySegment()
        {
            if (currentSegment == null)
                return false;

            var next = currentSegment.Next;
            while (next != null && next.Committed == 0)
                next = next.Next;

            if (next == null)
            {
                currentSegment = null;
                segmentConsumed = 0;
                return false;
            }

            currentSegment = next;
            segmentConsumed = 0;
            return true;
        }

        public T[] ToArray() => UnreadSequence.ToArray();

        /// <summary>
        /// 确定此读取器是否与另一个读取器相等（比较所属缓冲和已消费位置）。
        /// </summary>
        /// <param name="other">要比较的另一读取器。</param>
        /// <returns>若二者引用相同缓冲且已消费数相同则返回 true。</returns>
        public bool Equals(ValueSequenceBufferReader<T> other)
        {
            return Buffer == other.Buffer && Consumed == other.Consumed;
        }

        /// <summary>
        /// 相等运算符重载，比较两个读取器是否相等。
        /// </summary>
        public static bool operator ==(ValueSequenceBufferReader<T> left, ValueSequenceBufferReader<T> right)
            => left.Equals(right);

        /// <summary>
        /// 不等运算符重载，比较两个读取器是否不相等。
        /// </summary>
        public static bool operator !=(ValueSequenceBufferReader<T> left, ValueSequenceBufferReader<T> right)
            => !left.Equals(right);

        /// <summary>
        /// 重写的对象相等比较，支持与任意对象比较。
        /// </summary>
        public override bool Equals(object? obj)
            => obj is ValueSequenceBufferReader<T> other && Equals(other);

        /// <summary>
        /// 重写的哈希代码实现，基于底层缓冲引用和已消费位置生成。
        /// </summary>
        public override int GetHashCode()
            => HashCode.Combine(Buffer, Consumed);

        /// <summary>
        /// 返回此读取器的文本表示，包含已消费与剩余信息。
        /// </summary>
        public override string ToString()
            => $"SequenceBufferReader(Consumed={Consumed}, Remaining={Remaining}, IsCompleted={IsCompleted})";

        /// <summary>
        /// 释放读取器占用的引用：尝试释放/解冻底层序列以允许回收（若序列未被其它引用冻结）。
        /// </summary>
        public void Dispose()
        {
            Buffer.TryRelease();
        }

        /// <summary>
        /// 隐式从 <see cref="SequenceBuffer{T}"/> 创建一个读取器，构造时会冻结目标缓冲。
        /// </summary>
        public static implicit operator ValueSequenceBufferReader<T>(SequenceBuffer<T> buffer)
            => new ValueSequenceBufferReader<T>(buffer);

        /// <summary>
        /// 隐式将读取器转换为 <see cref="ReadOnlySequence{T}"/>，表示当前未读的数据序列。
        /// </summary>
        public static implicit operator ReadOnlySequence<T>(ValueSequenceBufferReader<T> reader)
            => reader.UnreadSequence;

        public static implicit operator SequenceBuffer<T>(ValueSequenceBufferReader<T> reader)
            => reader.Buffer;

        public static implicit operator ValueSequenceBufferReader<T>(SequenceBufferReader<T> reader)
        {
            ValueSequenceBufferReader<T> vReader = new(reader);
            vReader.Advance((int)reader.Consumed);
            return vReader;
        }

        public static implicit operator SequenceBufferReader<T>(ValueSequenceBufferReader<T> reader)
        {
            var sReader = SequenceBufferReader<T>.GetReader(reader);
            sReader.Advance((int)reader.Consumed);
            return sReader;
        }
    }
}