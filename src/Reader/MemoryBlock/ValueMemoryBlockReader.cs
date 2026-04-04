using System.Buffers;

namespace ExtenderApp.Buffer.Reader
{
    /// <summary>
    /// 针对单个 <see cref="MemoryBlock{T}"/> 的轻量值类型只向前读取器。
    /// 维护已消费偏移并提供多种便捷读取方法与视图（<see cref="ReadOnlySpan{T}"/> / <see cref="ReadOnlyMemory{T}"/> / <see cref="ArraySegment{T}"/> / <see cref="ReadOnlySequence{T}"/>）。
    /// 读取器不拥有底层 <see cref="MemoryBlock{T}"/> 的生命周期；在 <see cref="Dispose"/> / 隐式转换回托管读取器时会尝试调用块的释放以协助资源管理。
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    public struct ValueMemoryBlockReader<T> : IDisposable, IEquatable<ValueMemoryBlockReader<T>>
    {
        /// <summary>
        /// 被读取的内存块实例。读取器不会转移所有权，但在释放时会尝试通过 <see cref="MemoryBlock{T}.TryRelease"/> 协助回收。
        /// </summary>
        internal readonly MemoryBlock<T> Block;

        /// <summary>
        /// 已消费（已读取）的元素数量（相对于块的已提交区域起点）。
        /// </summary>
        public int Consumed { get; private set; }

        /// <summary>
        /// 使用指定 <paramref name="block"/> 创建一个读取器，初始已消费位置为 0。
        /// </summary>
        /// <param name="block">要读取的 <see cref="MemoryBlock{T}"/>，不能为空。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="block"/> 为 null 时抛出。</exception>
        public ValueMemoryBlockReader(MemoryBlock<T> block)
        {
            Block = block ?? throw new ArgumentNullException(nameof(block));
            Block.Freeze(); // 确保块不可变以安全读取
            Consumed = 0;
        }

        /// <summary>
        /// 剩余可读的元素数量（等于块的已提交长度减去当前已消费数）。
        /// </summary>
        public int Remaining => (int)(Block.Committed - Consumed);

        /// <summary>
        /// 指示读取器是否已读尽当前块的已提交数据。
        /// </summary>
        public bool IsCompleted => Remaining == 0;

        /// <summary>
        /// 从当前位置到已提交末尾的只读跨度视图。
        /// </summary>
        public ReadOnlySpan<T> CommittedSpan => Block.CommittedSpan.Slice(Consumed);

        /// <summary>
        /// 从当前位置到已提交末尾的只读内存视图。
        /// </summary>
        public ReadOnlyMemory<T> UnreadMemory => Block.CommittedMemory.Slice(Consumed);

        /// <summary>
        /// 从当前位置到已提交末尾的数组段视图。
        /// </summary>
        public ArraySegment<T> UnreadSegment => Block.CommittedSegment.Slice(Consumed);

        /// <summary>
        /// 预览下一个元素但不推进读取位置。
        /// </summary>
        /// <param name="item">当返回 true 时输出下一个元素。</param>
        /// <returns>若存在下一个元素则返回 true，否则返回 false。</returns>
        public bool TryPeek(out T item)
        {
            if (Remaining <= 0)
            {
                item = default!;
                return false;
            }

            item = CommittedSpan[0];
            return true;
        }

        /// <summary>
        /// 读取下一个元素并推进位置。
        /// </summary>
        /// <param name="item">当返回 true 时包含读取到的元素。</param>
        /// <returns>如成功读取返回 true，否则返回 false（例如已无数据）。</returns>
        public bool TryRead(out T item)
        {
            if (!TryPeek(out item))
                return false;

            Advance(1);
            return true;
        }

        /// <summary>
        /// 尝试读取并复制指定长度到目标 <see cref="Span{T}"/>，若剩余不足则返回 false 且不推进位置。
        /// </summary>
        /// <param name="destination">目标跨度，用于接收数据。</param>
        /// <returns>当成功复制并推进位置时返回 true；若剩余不足返回 false 且不改变状态。</returns>
        public bool TryRead(Span<T> destination)
        {
            if (destination.Length == 0)
                return true;

            if (Remaining < destination.Length)
                return false;

            CommittedSpan.Slice(0, destination.Length).CopyTo(destination);
            Advance(destination.Length);
            return true;
        }

        /// <summary>
        /// 读取下一个元素并推进位置；若无数据则抛出异常。
        /// </summary>
        /// <returns>读取到的元素。</returns>
        /// <exception cref="InvalidOperationException">当没有更多数据可读时抛出。</exception>
        public T Read()
        {
            if (Remaining <= 0)
                throw new InvalidOperationException("没有可读数据。");

            T result = CommittedSpan[0];
            Advance(1);
            return result;
        }

        /// <summary>
        /// 将尽可能多的数据复制到目标跨度并推进读取位置，返回实际复制的元素数量（可能小于目标长度）。
        /// </summary>
        /// <param name="destination">目标跨度。</param>
        /// <returns>实际复制并消费的元素数量。</returns>
        public int Read(Span<T> destination)
        {
            if (destination.Length == 0)
                return 0;

            int toCopy = Math.Min(Remaining, destination.Length);
            if (toCopy == 0)
                return 0;

            CommittedSpan.Slice(0, toCopy).CopyTo(destination);
            Advance(toCopy);
            return toCopy;
        }

        /// <summary>
        /// 将尽可能多的数据复制到目标 <see cref="Memory{T}"/> 并推进读取位置，返回实际复制的元素数量。
        /// </summary>
        /// <param name="destination">目标内存。</param>
        /// <returns>实际复制并消费的元素数量。</returns>
        public int Read(Memory<T> destination)
        {
            return Read(destination.Span);
        }

        /// <summary>
        /// 将尽可能多的数据复制到目标 <see cref="ArraySegment{T}"/> 并推进读取位置，返回实际复制的元素数量。
        /// </summary>
        /// <param name="destination">目标数组段。</param>
        /// <returns>实际复制并消费的元素数量。</returns>
        public int Read(ArraySegment<T> destination)
        {
            return Read(destination.AsSpan());
        }

        /// <summary>
        /// 将读取位置向前推进指定数量的元素。
        /// </summary>
        /// <param name="count">推进的元素数量（必须为非负且不超过剩余）。</param>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="count"/> 无效或超出可读范围时抛出。</exception>
        public void Advance(int count)
        {
            if (count < 0 || Consumed + count > Block.Committed)
                throw new ArgumentOutOfRangeException(nameof(count));

            Consumed += count;
        }

        /// <summary>
        /// 将读取位置回退指定数量的元素。
        /// </summary>
        /// <param name="count">回退的元素数量（必须为非负且不超过当前已消费）。</param>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="count"/> 无效或大于已消费数量时抛出。</exception>
        public void Rewind(int count)
        {
            if (count < 0 || Consumed - count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            Consumed -= count;
        }

        /// <summary>
        /// 将读取器重置为初始状态（已消费位置设为 0）。
        /// </summary>
        public void Reset()
        {
            Consumed = 0;
        }

        /// <summary>
        /// 比较两个 <see cref="ValueMemoryBlockReader{T}"/> 是否等价（同一块且已消费相同数量）。
        /// </summary>
        public bool Equals(ValueMemoryBlockReader<T> other)
        {
            return Block.Equals(other.Block) && Consumed == other.Consumed;
        }

        /// <summary>
        /// 相等运算符重载。
        /// </summary>
        public static bool operator ==(ValueMemoryBlockReader<T> left, ValueMemoryBlockReader<T> right)
            => left.Equals(right);

        /// <summary>
        /// 不等运算符重载。
        /// </summary>
        public static bool operator !=(ValueMemoryBlockReader<T> left, ValueMemoryBlockReader<T> right)
            => !left.Equals(right);

        /// <summary>
        /// 比较对象相等性。
        /// </summary>
        public override bool Equals(object? obj)
            => obj is ValueMemoryBlockReader<T> other && Equals(other);

        /// <summary>
        /// 生成哈希码。
        /// </summary>
        public override int GetHashCode()
            => HashCode.Combine(Block, Consumed);

        /// <summary>
        /// 返回便于诊断的字符串表示。
        /// </summary>
        public override string ToString()
            => $"MemoryBlockReader(Committed={Consumed}, Remaining={Remaining}, IsCompleted={IsCompleted})";

        /// <summary>
        /// 释放读取器占用的引用：尝试释放/解冻底层内存块以允许回收（若块未被其它引用冻结）。
        /// </summary>
        public void Dispose()
        {
            Block.TryRelease();
        }

        /// <summary>
        /// 隐式转换为底层 <see cref="MemoryBlock{T}"/>。
        /// </summary>
        /// <param name="reader">源读取器。</param>
        public static implicit operator MemoryBlock<T>(ValueMemoryBlockReader<T> reader)
            => reader.Block;

        /// <summary>
        /// 隐式转换为表示当前已提交未读部分的 <see cref="ReadOnlySpan{T}"/>。
        /// </summary>
        /// <param name="reader">源读取器。</param>
        public static implicit operator ReadOnlySpan<T>(ValueMemoryBlockReader<T> reader)
            => reader.CommittedSpan;

        /// <summary>
        /// 隐式转换为表示当前未读部分的 <see cref="ReadOnlyMemory{T}"/>.
        /// </summary>
        /// <param name="reader">源读取器。</param>
        public static implicit operator ReadOnlyMemory<T>(ValueMemoryBlockReader<T> reader)
            => reader.UnreadMemory;

        /// <summary>
        /// 隐式转换为表示当前未读部分的 <see cref="ArraySegment{T}"/>.
        /// </summary>
        /// <param name="reader">源读取器。</param>
        public static implicit operator ArraySegment<T>(ValueMemoryBlockReader<T> reader)
            => reader.UnreadSegment;

        /// <summary>
        /// 隐式转换为表示当前未读部分的 <see cref="ReadOnlySequence{T}"/>。
        /// </summary>
        /// <param name="reader">源读取器。</param>
        public static implicit operator ReadOnlySequence<T>(ValueMemoryBlockReader<T> reader)
            => new ReadOnlySequence<T>(reader.UnreadMemory);

        /// <summary>
        /// 将值类型读取器隐式转换为引用类型的 <see cref="MemoryBlockReader{T}"/>。
        /// 返回的引用读取器会绑定到相同底层块并推进到相同已消费位置。
        /// </summary>
        /// <param name="reader">源值读取器。</param>
        /// <returns>等效的 <see cref="MemoryBlockReader{T}"/> 实例。</returns>
        public static implicit operator MemoryBlockReader<T>(ValueMemoryBlockReader<T> reader)
        {
            var bReader = MemoryBlockReader<T>.GetReader(reader);
            bReader.Advance(reader.Consumed);
            return bReader;
        }

        /// <summary>
        /// 将引用类型的 <see cref="MemoryBlockReader{T}"/> 转为值类型读取器。
        /// 返回的值读取器绑定到相同底层块并推进到相同已消费位置。
        /// </summary>
        /// <param name="reader">源引用读取器。</param>
        /// <returns>等效的 <see cref="ValueMemoryBlockReader{T}"/> 实例。</returns>
        public static implicit operator ValueMemoryBlockReader<T>(MemoryBlockReader<T> reader)
        {
            var vReader = new ValueMemoryBlockReader<T>(reader);
            vReader.Advance((int)reader.Consumed);
            return vReader;
        }

        /// <summary>
        /// 将 <see cref="MemoryBlock{T}"/> 隐式转换为 <see cref="ValueMemoryBlockReader{T}"/>（位置从 0 开始）。
        /// </summary>
        /// <param name="block">源内存块。</param>
        public static implicit operator ValueMemoryBlockReader<T>(MemoryBlock<T> block)
            => new ValueMemoryBlockReader<T>(block);
    }
}