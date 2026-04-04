namespace ExtenderApp.Buffer.MemoryBlocks
{
    /// <summary>
    /// 基于固定数组的内存块提供者，用于创建或复用 <see cref="MemoryBlock{T}"/> 实例。
    /// 与 <see cref="ArrayPoolBlockProvider{T}"/> 不同：此处不经过 <see cref="System.Buffers.ArrayPool{T}"/>，底层为自有或调用方提供的数组。
    /// </summary>
    /// <typeparam name="T">内存块中元素的类型。</typeparam>
    internal sealed class FixedArrayBlockProvider<T> : MemoryBlockProvider<T>
    {
        private static readonly Lazy<FixedArrayBlockProvider<T>> _default = new(static () => new());

        /// <summary>
        /// 获取默认的 <see cref="FixedArrayBlockProvider{T}"/> 单例（延迟初始化）。
        /// </summary>
        public static FixedArrayBlockProvider<T> Default => _default.Value;

        private readonly ObjectPool<FixedArrayMemoryBlock> _blockPool = ObjectPool.Create<FixedArrayMemoryBlock>();

        /// <summary>
        /// 从内部对象池获取可写块并按 <paramref name="sizeHint"/> 分配元素空间。
        /// 当 <paramref name="sizeHint"/> 大于 0 时，新建固定长度数组并封装为可写内存块；为 0 时可返回仅经池复用外壳的空块。
        /// </summary>
        /// <param name="sizeHint">建议容量（元素个数）；0 表示可返回未装入数据的空块。</param>
        /// <returns>可写的 <see cref="MemoryBlock{T}"/>，由内部对象池管理生命周期。</returns>
        protected override sealed MemoryBlock<T> CreateBufferProtected(int sizeHint)
        {
            var block = _blockPool.Get();
            block.Segment = new ArraySegment<T>(new T[sizeHint]);
            block.Initialize(this);
            return block;
        }

        /// <summary>
        /// 将已有数组整段装入内存块；调用方保留数组所有权，释放内存块时不会归还或修改该数组。
        /// </summary>
        /// <param name="array">要装入的数组，不能为 null。</param>
        /// <returns>封装该数组的 <see cref="MemoryBlock{T}"/>，由内部对象池管理外壳。</returns>
        public MemoryBlock<T> GetBuffer(T[] array)
        {
            return GetBuffer(new ArraySegment<T>(array));
        }

        /// <summary>
        /// 将已有数组的指定区间装入内存块；调用方保留数组所有权，释放内存块时不会归还或修改该区间。
        /// </summary>
        /// <param name="array">要装入的数组，不能为 null。</param>
        /// <param name="start">起始索引（从 0 起算）。</param>
        /// <param name="length">区间长度。</param>
        /// <returns>封装该数组段的 <see cref="MemoryBlock{T}"/>，由内部对象池管理外壳。</returns>
        public MemoryBlock<T> GetBuffer(T[] array, int start, int length)
        {
            return GetBuffer(new ArraySegment<T>(array, start, length));
        }

        /// <summary>
        /// 将指定的 <see cref="ArraySegment{T}"/> 装入内存块，并按段长度推进已提交可写长度（与仅按容量新建空块的路径不同）。
        /// </summary>
        /// <param name="segment">要装入的数组段，须为有效段。</param>
        /// <returns>封装该数组段的 <see cref="MemoryBlock{T}"/>，由内部对象池管理外壳。</returns>
        public MemoryBlock<T> GetBuffer(ArraySegment<T> segment)
        {
            var block = _blockPool.Get();
            block.Segment = segment;
            block.Initialize(this);
            block.Advance(segment.Count);
            return block;
        }

        /// <summary>
        /// 将内存块归还内部对象池；非本提供者类型的块则直接释放。
        /// </summary>
        protected override sealed void ReleaseProtected(MemoryBlock<T> buffer)
        {
            if (buffer is FixedArrayMemoryBlock fixedBlock)
            {
                fixedBlock.Segment = default;
                _blockPool.Release(fixedBlock);
            }
            else
            {
                buffer.Dispose();
            }
        }

        /// <summary>
        /// 内部固定数组内存块：封装固定大小的自有或外部数组，不复制数据。实例由本提供者的对象池复用，不应在外部直接 new。
        /// </summary>
        private sealed class FixedArrayMemoryBlock : MemoryBlock<T>
        {
            /// <summary>
            /// 当前装入的数组段，即底层有效内存范围。
            /// </summary>
            public ArraySegment<T> Segment;

            /// <summary>
            /// 创建未装入任何数组段的 <see cref="FixedArrayMemoryBlock"/>。
            /// </summary>
            public FixedArrayMemoryBlock()
            {
                Segment = default!;
            }

            /// <summary>
            /// 返回当前可用的底层内存（固定映射到 <see cref="Segment"/>）。
            /// </summary>
            protected override sealed Memory<T> AvailableMemory => Segment;

            /// <summary>
            /// 固定数组不可扩容；当请求的 <paramref name="sizeHint"/> 超过剩余可写空间时抛出 <see cref="InvalidOperationException"/>。
            /// </summary>
            /// <param name="sizeHint">请求的最小可写元素容量。</param>
            /// <exception cref="InvalidOperationException">底层缓冲大小固定，任何扩容尝试均会抛出。</exception>
            protected override sealed void EnsureCapacityProtected(int sizeHint)
            {
                throw new InvalidOperationException("底层缓冲为固定大小，无法超出容量扩展可写空间。");
            }
        }
    }
}
