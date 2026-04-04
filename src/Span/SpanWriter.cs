using System.Runtime.CompilerServices;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 针对 <see cref="Span{T}"/> 的轻量写入器（栈上类型）。 提供对底层跨度的顺序写入操作（零拷贝），适用于在栈上构造/填充数据元素序列。
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    public ref struct SpanWriter<T>
    {
        /// <summary>
        /// 需要写入的目标跨度，写入操作会修改该跨度的内容但不改变其长度。
        /// </summary>
        private Span<T> span;

        /// <summary>
        /// 当前写入位置（相对于起点的元素数）。写入操作会推进该位置，表示已写入的元素数。初始值为 0，最大值不超过跨度长度。
        /// </summary>
        private int position;

        /// <summary>
        /// 使用指定可写字节跨度创建写入器，初始已写入为 0。
        /// </summary>
        /// <param name="span">目标字节跨度。</param>
        public SpanWriter(Span<T> span)
        {
            this.span = span;
            position = 0;
        }

        /// <summary>
        /// 当前已写入的元素数（相对于起点）。
        /// </summary>
        public int Consumed => position;

        /// <summary>
        /// 剩余可写元素数。
        /// </summary>
        public int Remaining => span.Length - position;

        /// <summary>
        /// 指示是否已写满（无剩余空间）。
        /// </summary>
        public bool IsCompleted => Remaining == 0;

        /// <summary>
        /// 获取当前未写部分的视图（不推进位置）。
        /// </summary>
        public Span<T> UnwrittenSpan => span.Slice(position);

        /// <summary>
        /// 尝试写入单个元素，写入成功则推进位置并返回 <c>true</c>；否则返回 <c>false</c> 且不改变位置。
        /// </summary>
        /// <param name="value">要写入的元素值。</param>
        /// <returns>写入成功则为 <c>true</c>，否则为 <c>false</c>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWrite(T value)
        {
            if (Remaining < 1)
                return false;
            span[position] = value;
            Advance(1);
            return true;
        }

        /// <summary>
        /// 写入单个元素，若剩余不足则抛出异常。
        /// </summary>
        /// <param name="value">要写入的元素值。</param>
        /// <exception cref="InvalidOperationException">当目标跨度空间不足时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(T value)
        {
            if (!TryWrite(value))
                throw new InvalidOperationException("目标跨度空间不足，无法写入字节。");
        }

        /// <summary>
        /// 尝试将指定源数据复制到目标跨度并推进位置；若剩余不足则返回 <c>false</c> 且不改变位置。
        /// </summary>
        /// <param name="source">源只读跨度。</param>
        /// <returns>复制成功并推进位置则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWrite(ReadOnlySpan<T> source)
        {
            if (source.Length == 0)
                return true;
            if (Remaining < source.Length)
                return false;
            source.CopyTo(span.Slice(position, source.Length));
            Advance(source.Length);
            return true;
        }

        /// <summary>
        /// 将指定源数据写入目标跨度，若剩余不足则抛出异常。
        /// </summary>
        /// <param name="source">源只读跨度。</param>
        /// <exception cref="InvalidOperationException">当目标跨度空间不足时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ReadOnlySpan<T> source)
        {
            if (!TryWrite(source))
                throw new InvalidOperationException("目标跨度空间不足，无法写入数据。");
        }

        /// <summary>
        /// 推进写入位置指定的元素数。
        /// </summary>
        /// <param name="count">推进的元素数，必须为非负且不超过剩余。</param>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="count"/> 无效时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            if (count < 0 || position + count > span.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            position += count;
        }

        /// <summary>
        /// 将写入位置回退指定元素数。
        /// </summary>
        /// <param name="count">回退的元素数（非负且不大于已写入）。</param>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="count"/> 无效时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rewind(int count)
        {
            if (count < 0 || position - count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            position -= count;
        }

        /// <summary>
        /// 重置写入位置为起点。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset() => position = 0;

        /// <summary>
        /// 从 <see cref="Span{T}"/> 隐式创建写入器。
        /// </summary>
        /// <param name="span">源可写跨度。</param>
        public static implicit operator SpanWriter<T>(Span<T> span)
            => new(span);

        /// <summary>
        /// 隐式转换为尚未写入部分的 <see cref="Span{T}"/> 视图。
        /// </summary>
        /// <param name="writer">源写入器实例。</param>
        public static implicit operator Span<T>(SpanWriter<T> writer)
            => writer.UnwrittenSpan;

        /// <summary>
        /// 便于诊断的字符串表示。
        /// </summary>
        public override string ToString() => $"SpanWriter(Consumed={position}, Remaining={Remaining}, IsCompleted={IsCompleted})";
    }
}