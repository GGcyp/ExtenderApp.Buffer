using ExtenderApp.Buffer.MemoryBlocks;

namespace ExtenderApp.Buffer
{
    public partial class MemoryBlock<T>
    {
        /// <summary>
        /// 创建一个新的可写内存块，建议的初始容量为 <paramref name="initialCapacity"/>。
        /// </summary>
        /// <param name="initialCapacity">建议的初始容量（元素数量）。实现可将其作为预分配或增长的提示。</param>
        /// <returns>返回一个已初始化的 <see cref="MemoryBlock{T}"/> 实例。返回的实例可能来自内部池，调用方在不再使用时应按约定释放或归还该内存块（例如调用 <see cref="Dispose"/> 或相关的释放方法）。</returns>
        public static MemoryBlock<T> GetBuffer(int initialCapacity = 16)
            => ArrayPoolBlockProvider<T>.Default.GetBuffer(initialCapacity);

        /// <summary>
        /// 从指定的可写跨度创建并返回一个内存块，并将 <paramref name="span"/> 的内容写入该内存块。
        /// </summary>
        /// <param name="span">用于初始化内存块的数据可写跨度（数据将被复制到返回的内存块中）。</param>
        /// <returns>返回包含复制数据的 <see cref="MemoryBlock{T}"/> 实例。调用方在不再使用时应释放或归还该实例。</returns>
        public static MemoryBlock<T> GetBuffer(Span<T> span)
            => ArrayPoolBlockProvider<T>.Default.GetBuffer(span);

        /// <summary>
        /// 从指定的只读跨度创建并返回一个内存块，并将 <paramref name="span"/> 的内容写入该内存块。
        /// </summary>
        /// <param name="span">用于初始化内存块的数据只读跨度（数据将被复制到返回的内存块中）。</param>
        /// <returns>返回包含复制数据的 <see cref="MemoryBlock{T}"/> 实例。调用方在不再使用时应释放或归还该实例。</returns>
        public static MemoryBlock<T> GetBuffer(ReadOnlySpan<T> span)
            => ArrayPoolBlockProvider<T>.Default.GetBuffer(span);

        /// <summary>
        /// 将指定的 <see cref="Memory{T}"/> 包装为固定内存块并返回（通常不进行数据复制）。
        /// </summary>
        /// <param name="memory">要包装的内存。返回的内存块会直接引用该内存。</param>
        /// <returns>返回包装了指定内存的 <see cref="MemoryBlock{T}"/>。调用方负责保证被包装内存在使用期间有效，并在不再使用时按约定释放返回的内存块。</returns>
        /// <remarks>该方法通常不会复制内存，因此返回的内存块与原始内存共享底层数据，调用方应注意所有权与并发访问语义。</remarks>
        public static MemoryBlock<T> GetBuffer(Memory<T> memory)
            => FixedMemoryBlockProvider<T>.Default.GetBuffer(memory);

        /// <summary>
        /// 将指定的 <see cref="ReadOnlyMemory{T}"/> 包装为固定内存块并返回（通常不进行数据复制）。
        /// </summary>
        /// <param name="memory">要包装的只读内存。返回的内存块会直接引用该内存。</param>
        /// <returns>返回包装了指定只读内存的 <see cref="MemoryBlock{T}"/>。调用方负责保证被包装内存在使用期间有效，并在不再使用时按约定释放返回的内存块。</returns>
        /// <remarks>该方法通常不会复制内存，因此返回的内存块与原始内存共享底层数据，调用方应注意所有权与并发访问语义。</remarks>
        public static MemoryBlock<T> GetBuffer(ReadOnlyMemory<T> memory)
            => FixedMemoryBlockProvider<T>.Default.GetBuffer(memory);

        /// <summary>
        /// 将指定的数组包装为固定数组块并返回（不复制数组）。
        /// </summary>
        /// <param name="array">要包装的数组。</param>
        /// <returns>返回包装了指定数组的 <see cref="MemoryBlock{T}"/>。调用方在不再使用时应释放或归还该内存块。</returns>
        public static MemoryBlock<T> GetBuffer(T[] array)
            => FixedArrayBlockProvider<T>.Default.GetBuffer(array);

        /// <summary>
        /// 将指定数组的子范围（起始索引和长度）包装为固定数组块并返回（不复制数组）。
        /// </summary>
        /// <param name="array">要包装的数组。</param>
        /// <param name="start">子范围的起始索引。</param>
        /// <param name="length">子范围的长度。</param>
        /// <returns>返回包装了数组子范围的 <see cref="MemoryBlock{T}"/>。调用方在不再使用时应释放或归还该内存块。</returns>
        public static MemoryBlock<T> GetBuffer(T[] array, int start, int length)
            => FixedArrayBlockProvider<T>.Default.GetBuffer(array, start, length);

        /// <summary>
        /// 将指定的 <see cref="ArraySegment{T}"/> 包装为固定数组块并返回（不复制数组）。
        /// </summary>
        /// <param name="segment">要包装的数组段。</param>
        /// <returns>返回包装了数组段的 <see cref="MemoryBlock{T}"/>。调用方在不再使用时应释放或归还该内存块。</returns>
        public static MemoryBlock<T> GetBuffer(ArraySegment<T> segment)
            => FixedArrayBlockProvider<T>.Default.GetBuffer(segment);
    }
}