using System.Buffers;

namespace ExtenderApp.Buffer.Reader
{
    /// <summary>
    /// 用于读取 <see cref="MemoryBlock{T}"/> 已提交数据的读取器实现。
    /// 继承自 <see cref="AbstractBufferReader{T}"/>，提供针对 <see cref="MemoryBlock{T}"/> 的便捷视图（<see cref="ReadOnlySequence{T}"/>, <see cref="ReadOnlySpan{T}"/>, <see cref="ReadOnlyMemory{T}"/>）。
    /// 实例一般由对应的 <see cref="MemoryBlockReaderProvider{T}"/> 创建或回收。
    /// </summary>
    /// <typeparam name="T">缓冲区元素类型。</typeparam>
    public class MemoryBlockReader<T> : AbstractBufferReader<T>
    {
        /// <summary>
        /// 获取一个新的 <see cref="MemoryBlockReader{T}"/> 实例以读取指定的 <see cref="MemoryBlock{T}"/>。
        /// </summary>
        /// <param name="buffer">需要读取的内存块实例。</param>
        /// <returns>内存块读取器实例。</returns>
        public static MemoryBlockReader<T> GetReader(MemoryBlock<T> buffer) => (MemoryBlockReader<T>)MemoryBlockReaderProvider<T>.Default.GetReader(buffer);

        /// <summary>
        /// 内部使用的读取器提供者引用（用于回收/复用）。
        /// 使用 <c>new</c> 隐藏基类成员，仅供同程序集或提供者实现访问。
        /// </summary>
        internal new MemoryBlockReaderProvider<T>? ReaderProvider { get; set; }

        /// <summary>
        /// 当前绑定的 <see cref="MemoryBlock{T}"/> 缓冲区实例。
        /// 该属性通过包装基类的 <see cref="AbstractBufferReader{T}.Buffer"/> 提供强类型访问。
        /// </summary>
        public new MemoryBlock<T> Buffer => (MemoryBlock<T>)base.Buffer;

        /// <summary>
        /// 返回从当前已消费位置到缓冲区已提交末尾的只读序列视图。
        /// </summary>
        public override ReadOnlySequence<T> UnreadSequence => Buffer.CommittedSequence.Slice((int)Consumed);

        /// <summary>
        /// 返回从当前已消费位置到已提交末尾的只读跨度（若数据在单一连续内存中则不产生分段）。
        /// </summary>
        public ReadOnlySpan<T> UnreadSpan => Buffer.CommittedSpan.Slice((int)Consumed);

        /// <summary>
        /// 返回从当前已消费位置到已提交末尾的只读内存视图。
        /// </summary>
        public ReadOnlyMemory<T> UnreadMemory => Buffer.CommittedMemory.Slice((int)Consumed);

        /// <summary>
        /// 返回从当前消费位置到已提交末尾的只读内存视图。
        /// </summary>
        public ArraySegment<T> UnreadSegment => Buffer.CommittedSegment.Slice((int)Consumed);

        /// <summary>
        /// 创建一个未绑定到具体缓冲区的 <see cref="MemoryBlockReader{T}"/> 实例。
        /// 提供者在分配后会设置 <see cref="Buffer"/>（通过基类 Initialize）。
        /// </summary>
        public MemoryBlockReader()
        {
            // base constructor already initializes state
        }

        /// <summary>
        /// 尝试从当前未读位置读取指定数量的元素到只读跨度 <paramref name="span"/> 中。
        /// 若 <paramref name="count"/> 为非正值或剩余元素不足则返回 <c>false</c>，且不推进读取位置。
        /// 成功时会将对应元素切片赋给 <paramref name="span"/> 并推进已消费位置 <see cref="Consumed"/>。
        /// </summary>
        /// <param name="count">要读取的元素数量，必须为正整数。</param>
        /// <param name="span">输出的只读跨度，成功时包含读取到的数据片段；失败时为 default。</param>
        /// <returns>若成功读取并推进位置则返回 <c>true</c>，否则返回 <c>false</c>（例如 <paramref name="count"/> 非正或剩余不足）。</returns>
        public bool TryRead(int count, out ReadOnlySpan<T> span)
        {
            if (count <= 0 || Remaining < count)
            {
                span = default;
                return false;
            }
            span = UnreadSpan.Slice(0, count);
            Advance(count);
            return true;
        }

        /// <summary>
        /// 尝试从当前未读位置读取指定数量的元素到只读内存 <paramref name="memory"/> 中。
        /// 若 <paramref name="count"/> 为非正值或剩余元素不足则返回 <c>false</c>，且不推进读取位置。
        /// 成功时会将对应元素切片赋给 <paramref name="memory"/> 并推进已消费位置 <see cref="Consumed"/>。
        /// </summary>
        /// <param name="count">要读取的元素数量，必须为正整数。</param>
        /// <param name="memory">输出的只读内存，成功时包含读取到的数据片段；失败时为 default。</param>
        /// <returns>若成功读取并推进位置则返回 <c>true</c>，否则返回 <c>false</c>（例如 <paramref name="count"/> 非正或剩余不足）。</returns>
        public bool TryRead(int count, out ReadOnlyMemory<T> memory)
        {
            if (count <= 0 || Remaining < count)
            {
                memory = default;
                return false;
            }
            memory = UnreadMemory.Slice(0, count);
            Advance(count);
            return true;
        }

        /// <summary>
        /// 尝试从当前未读位置读取指定数量的元素到数组段 <paramref name="segment"/> 中。
        /// 若 <paramref name="count"/> 为非正值或剩余元素不足则返回 <c>false</c>，且不推进读取位置。
        /// 成功时会将对应元素切片赋给 <paramref name="segment"/> 并推进已消费位置 <see cref="Consumed"/>。
        /// </summary>
        /// <param name="count">要读取的元素数量，必须为正整数。</param>
        /// <param name="segment">输出的数组段，成功时包含读取到的数据片段；失败时为 default。</param>
        /// <returns>若成功读取并推进位置则返回 <c>true</c>，否则返回 <c>false</c>（例如 <paramref name="count"/> 非正或剩余不足）。</returns>
        public bool TryRead(int count, out ArraySegment<T> segment)
        {
            if (count <= 0 || Remaining < count)
            {
                segment = default;
                return false;
            }
            segment = UnreadSegment.Slice(0, count);
            Advance(count);
            return true;
        }

        /// <summary>
        /// 从当前未读位置读取指定数量的元素并返回一个只读跨度。
        /// </summary>
        /// <param name="count">要读取的元素数量，必须为正整数。</param>
        /// <returns>读取成功后的切片。</returns>
        /// <exception cref="ArgumentOutOfRangeException">当需要读取的范围超出限制后抛出</exception>
        public ReadOnlySpan<T> Read(int count)
        {
            if (count <= 0 || Remaining < count)
                throw new ArgumentOutOfRangeException(nameof(count), "读取数量不能低于 0 或者大于未读数量");

            var span = UnreadSpan.Slice(0, count);
            Advance(count);
            return span;
        }

        /// <summary>
        /// 将读取器隐式转换为表示当前未读数据的 <see cref="ReadOnlyMemory{T}"/>。
        /// 等价于访问 <see cref="UnreadMemory"/>。
        /// </summary>
        /// <param name="reader">源读取器实例。</param>
        public static implicit operator ReadOnlyMemory<T>(MemoryBlockReader<T> reader)
            => reader.UnreadMemory;

        /// <summary>
        /// 将读取器隐式转换为表示当前未读数据的 <see cref="ReadOnlySpan{T}"/>。
        /// 等价于访问 <see cref="UnreadSpan"/>。
        /// </summary>
        /// <param name="reader">源读取器实例。</param>
        public static implicit operator ReadOnlySpan<T>(MemoryBlockReader<T> reader)
            => reader.UnreadSpan;

        /// <summary>
        /// 将读取器隐式转换为其绑定的 <see cref="MemoryBlock{T}"/> 实例（可能为未初始化状态）。
        /// 等价于访问 <see cref="Buffer"/>。
        /// </summary>
        /// <param name="reader">源读取器实例。</param>
        public static implicit operator MemoryBlock<T>(MemoryBlockReader<T> reader)
            => reader.Buffer;

        /// <summary>
        /// 将读取器隐式转换为表示当前未读数据的 <see cref="ArraySegment{T}"/>。
        /// </summary>
        /// <param name="reader">源读取器实例。</param>
        public static implicit operator ArraySegment<T>(MemoryBlockReader<T> reader)
            => reader.UnreadSegment;
    }
}