using System.Runtime.CompilerServices;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 针对 <see cref="ReadOnlySpan{T}"/> 的轻量只向前读取器（栈上类型）。
    /// 提供对底层只读跨度的便捷读取、切片与位置推进操作。
    /// 该类型为 <c>ref struct</c>，不能装箱或存储在托管堆上。
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    public ref struct SpanReader<T>
    {
        /// <summary>
        /// 需要被读取的只读跨度，始终保持不变。通过 <see cref="consumed"/> 变量跟踪当前已消费位置。
        /// </summary>
        private readonly ReadOnlySpan<T> span;

        /// <summary>
        /// 当前已消费的元素数（相对于起点）。必须保证在 [0, span.Committed] 范围内，且推进/回退操作必须保持该不变式。
        /// </summary>
        private int consumed;

        /// <summary>
        /// 使用指定的只读跨度创建读取器，初始已消费为 0。
        /// </summary>
        /// <param name="span">要读取的只读跨度。</param>
        public SpanReader(ReadOnlySpan<T> span)
        {
            this.span = span;
            consumed = 0;
        }

        /// <summary>
        /// 当前已消费的元素数（相对于起点）。
        /// </summary>
        public int Consumed => consumed;

        /// <summary>
        /// 剩余可读元素数。
        /// </summary>
        public int Remaining => span.Length - consumed;

        /// <summary>
        /// 指示是否已读尽可用数据。
        /// </summary>
        public bool IsCompleted => Remaining == 0;

        /// <summary>
        /// 从当前已消费位置到末尾的只读跨度视图（未推进）。
        /// </summary>
        public ReadOnlySpan<T> UnreadSpan => span.Slice(consumed);

        /// <summary>
        /// 预览下一个元素但不推进位置。
        /// </summary>
        /// <param name="value">若存在下一个元素则输出该值。</param>
        /// <returns>存在下一个元素则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out T value)
        {
            if (Remaining <= 0)
            {
                value = default;
                return false;
            }
            value = span[consumed];
            return true;
        }

        /// <summary>
        /// 读取并返回下一个元素，失败不推进并返回 false。
        /// </summary>
        /// <param name="value">当返回 <c>true</c> 时输出读取到的元素。</param>
        /// <returns>成功读取返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRead(out T value)
        {
            if (!TryPeek(out value))
                return false;
            Advance(1);
            return true;
        }

        /// <summary>
        /// 尝试将指定长度的数据复制到目标跨度，若剩余不足则返回 <c>false</c> 且不推进位置。
        /// </summary>
        /// <param name="destination">目标跨度，用于接收复制的数据。</param>
        /// <returns>复制成功并推进位置则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRead(scoped Span<T> destination)
        {
            if (destination.Length == 0)
                return true;
            if (Remaining < destination.Length)
                return false;
            span.Slice(consumed, destination.Length).CopyTo(destination);
            Advance(destination.Length);
            return true;
        }

        /// <summary>
        /// 读取并返回下一个元素；若无数据则抛出异常。
        /// </summary>
        /// <returns>读取到的元素。</returns>
        /// <exception cref="InvalidOperationException">当没有更多数据可读时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read()
        {
            if (Remaining <= 0)
                throw new InvalidOperationException("没有可读数据。");
            var v = span[consumed];
            Advance(1);
            return v;
        }

        /// <summary>
        /// 将尽可能多的数据复制到目标跨度并推进位置，返回实际复制的元素数。
        /// </summary>
        /// <param name="destination">目标跨度。</param>
        /// <returns>实际复制并消费的元素数。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(scoped Span<T> destination)
        {
            if (destination.Length == 0)
                return 0;
            int toCopy = Math.Min(Remaining, destination.Length);
            if (toCopy == 0)
                return 0;
            span.Slice(consumed, toCopy).CopyTo(destination);
            Advance(toCopy);
            return toCopy;
        }

        /// <summary>
        /// 将读取位置向前推进指定元素数。
        /// </summary>
        /// <param name="count">推进的元素数，必须为非负且不超过剩余。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            if (count < 0 || consumed + count > span.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            consumed += count;
        }

        /// <summary>
        /// 将读取位置回退指定元素数。
        /// </summary>
        /// <param name="count">回退的元素数（非负且不超过已消费）。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rewind(int count)
        {
            if (count < 0 || consumed - count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            consumed -= count;
        }

        /// <summary>
        /// 重置已消费位置为 0。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset() => consumed = 0;

        /// <summary>
        /// 隐式转换为表示当前未读部分的 <see cref="ReadOnlySpan{T}"/>。
        /// </summary>
        /// <param name="reader">源读取器实例。</param>
        public static implicit operator ReadOnlySpan<T>(SpanReader<T> reader)
            => reader.UnreadSpan;

        /// <summary>
        /// 从只读跨度隐式创建读取器。
        /// </summary>
        /// <param name="span">源只读跨度。</param>
        public static implicit operator SpanReader<T>(ReadOnlySpan<T> span)
            => new(span);

        /// <summary>
        /// 从可写跨度隐式创建读取器。
        /// </summary>
        /// <param name="span">源可写跨度。</param>
        public static implicit operator SpanReader<T>(Span<T> span)
            => new(span);

        /// <summary>
        /// 返回便于诊断的字符串表示。
        /// </summary>
        public override string ToString() => $"SpanReader(Consumed={consumed}, Remaining={Remaining}, IsCompleted={IsCompleted})";
    }
}