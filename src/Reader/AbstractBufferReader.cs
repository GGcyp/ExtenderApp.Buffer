using System.Buffers;
using ExtenderApp.Buffer.Primitives;
using ExtenderApp.Buffer.Reader;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 通用的只读缓冲区读取器，基于 <see cref="AbstractBuffer{T}"/> 的已提交序列实现简单的前向读取操作。 构造时会对目标缓冲区调用 <see cref="FreezeObject.Freeze"/> 以防止在被引用时被回收；使用结束后应调用 <see
    /// cref="Dispose"/> 以尝试释放/解冻缓冲区引用。
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    public abstract class AbstractBufferReader<T> : DisposableObject
    {
        /// <summary>
        /// 读取器提供者实例
        /// </summary>
        protected virtual AbstractBufferReaderProvider<T> ReaderProvider { get; set; }

        /// <summary>
        /// 被读取的缓冲区实例（只读）。
        /// </summary>
        public virtual AbstractBuffer<T> Buffer { get; protected set; }

        /// <summary>
        /// 已消费（已读取）的总元素数（相对于序列起点）。
        /// </summary>
        public long Consumed { get; protected set; }

        /// <summary>
        /// 当前缓冲区已提交的总元素数（相对于序列起点）。读取器只能读取到这个位置之前的数据，不能跨越已提交边界。
        /// </summary>
        public long Committed => Buffer.Committed;

        /// <summary>
        /// 剩余可读的元素数量（等于缓冲区已提交长度减去当前已消费数）。
        /// </summary>
        public long Remaining => Committed - Consumed;

        /// <summary>
        /// 指示是否已读尽缓冲区的已提交数据。
        /// </summary>
        public bool IsCompleted => Remaining == 0;

        /// <summary>
        /// 返回当前未读取的只读序列视图（从当前已消费位置到已提交末尾）。 若已无未读数据则返回 <see cref="ReadOnlySequence{T}.Empty"/>.
        /// </summary>
        public abstract ReadOnlySequence<T> UnreadSequence { get; }

        /// <summary>
        /// 创建一个针对指定缓冲区的读取器，初始读取位置为序列起点（0）。
        /// </summary>
        /// <param name="buffer">要读取的缓冲区，不能为空。</param>
        public AbstractBufferReader()
        {
            Consumed = 0;
            Buffer = default!;
            ReaderProvider = default!;
        }

        /// <summary>
        /// 初始化此读取器以绑定到指定缓冲区。会冻结缓冲区的引用（防止回收）并冻结写入以阻止并发写入。 Provider 在分配读取器后应调用此方法。
        /// </summary>
        /// <param name="buffer">要绑定的缓冲区。</param>
        protected internal virtual void Initialize(AbstractBufferReaderProvider<T> provider, AbstractBuffer<T> buffer)
        {
            ReaderProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Consumed = 0;

            // 冻结缓冲区以防止被回收，并冻结写入以防止写入侧在读取期间修改数据
            Buffer.Freeze();
            Buffer.FreezeWrite();
        }

        /// <summary>
        /// 尝试预览下一个元素而不推进读取位置。
        /// </summary>
        /// <param name="item">输出元素（当返回 true 时有效）。</param>
        /// <returns>若存在下一个元素则返回 true，否则返回 false。</returns>
        public bool TryPeek(out T item)
        {
            if (Remaining <= 0)
            {
                item = default!;
                return false;
            }

            // 直接使用 FirstSpan 以避免跨段分配；当序列为空或跨段时 FirstSpan 会返回首段
            item = UnreadSequence.Slice(0, 1).FirstSpan[0];
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

            UnreadSequence.Slice(0, destination.Length).CopyTo(destination);
            Advance(destination.Length);
            return true;
        }

        /// <summary>
        /// 读取指定数量的元素并返回对应的只读序列片段；若剩余不足则返回 false 且不推进位置。
        /// </summary>
        /// <param name="count">要读取的元素数量。</param>
        /// <param name="sequence">输出的只读序列。</param>
        /// <returns>是否成功。</returns>
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
            if (destination.Length == 0 || Remaining == 0)
                return 0;

            int toRead = (int)Math.Min(Remaining, destination.Length);
            UnreadSequence.Slice(0, toRead).CopyTo(destination);
            Advance(toRead);
            return toRead;
        }

        /// <summary>
        /// 将尽可能多的数据复制到目标内存并推进读取位置，返回实际复制的元素数量。
        /// </summary>
        public int Read(Memory<T> destination) => Read(destination.Span);

        /// <summary>
        /// 将读取位置向前推进指定数量的元素（不跨越已提交长度边界）。
        /// </summary>
        /// <param name="count">推进的元素数量（必须为非负且不超过剩余）。</param>
        public virtual void Advance(long count)
        {
            if (count < 0 || Consumed + count > Buffer.Committed)
                throw new ArgumentOutOfRangeException(nameof(count));

            Consumed += count;
        }

        /// <summary>
        /// 将读取位置回退指定数量的元素（可以跨段回退）。
        /// </summary>
        /// <param name="count">回退的元素数量（必须为非负且不超过当前已消费）。</param>
        public void Rewind(long count)
        {
            if (count < 0 || count > Consumed)
                throw new ArgumentOutOfRangeException(nameof(count));

            Consumed -= count;
        }

        /// <summary>
        /// 重置读取器（已消费位置设为 0，回到序列起点）。
        /// </summary>
        public void Reset()
        {
            Buffer.UnfreezeWrite();
            Buffer.TryRelease();
            Buffer = default!;
            Consumed = 0;
        }

        public virtual void Release()
        {
            Reset();
            ReaderProvider.Release(this);
        }

        /// <summary>
        /// 将当前未读部分转换为数组。
        /// </summary>
        public T[] ToArray() => UnreadSequence.ToArray();

        public override string ToString()
            => $"BufferReader(Consumed={Consumed}, Remaining={Remaining}, IsCompleted={IsCompleted})";

        /// <summary>
        /// 释放读取器占用的引用：尝试释放/解冻底层缓冲以允许回收（若缓冲区未被其它引用冻结）。
        /// </summary>
        protected override void DisposeManagedResources()
        {
            if (Buffer != null)
            {
                // 确保写入冻结被解除
                Buffer.UnfreezeWrite();
                Buffer.TryRelease();
            }
        }

        /// <summary>
        /// 隐式转换为 <see cref="ReadOnlySequence{T}"/>，表示当前未读序列。
        /// </summary>
        /// <param name="reader">源读取器。</param>
        public static implicit operator ReadOnlySequence<T>(AbstractBufferReader<T> reader)
            => reader.UnreadSequence;

        public static implicit operator AbstractBuffer<T>(AbstractBufferReader<T> reader)
            => reader.Buffer;
    }
}