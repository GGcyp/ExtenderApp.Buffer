using ExtenderApp.Buffer.MemoryBlocks;

namespace ExtenderApp.Buffer.SequenceBuffers
{
    /// <summary>
    /// 为序列缓冲段提供创建/封装逻辑的抽象工厂。
    /// </summary>
    /// <remarks>
    /// 该工厂通过内部的 <see cref="MemoryBlockProvider{T}"/> 获取底层的 <see cref="MemoryBlock{T}"/> 并将其转换为 <see cref="SequenceBufferSegment{T}"/>。
    /// 派生类负责实现具体的段包装策略（见 <see cref="GetSegmentProtected(MemoryBlock{T})"/>）以及释放策略（见 <see cref="ReleaseSegmentProtected(SequenceBufferSegment{T})"/>）。
    /// </remarks>
    /// <typeparam name="T">段中元素的类型。</typeparam>
    public sealed class SequenceBufferSegmentProvider<T>
    {
        private static readonly Lazy<SequenceBufferSegmentProvider<T>> _default =
            new(static () => new());

        /// <summary>
        /// 默认共享的段提供者实例，指向基于内存块实现的提供者（由具体实现初始化）。
        /// </summary>
        public static SequenceBufferSegmentProvider<T> Shared = _default.Value;

        private readonly ObjectPool<SequenceBufferSegment<T>> _pool =
            ObjectPool.Create<SequenceBufferSegment<T>>();

        /// <summary>
        /// 内部的内存块提供者，用于获取底层的 <see cref="MemoryBlock{T}"/> 实例以供封装成序列段。
        /// </summary>
        private readonly MemoryBlockProvider<T> _provider;

        public SequenceBufferSegmentProvider() : this(MemoryBlockProvider<T>.Shared)
        {
        }

        /// <summary>
        /// 使用指定的内存块提供者创建 <see cref="SequenceBufferSegmentProvider{T}"/> 实例。
        /// </summary>
        /// <param name="provider">用于提供底层 <see cref="MemoryBlock{T}"/> 的缓冲工厂，不能为空。</param>
        /// <exception cref="ArgumentNullException"><paramref name="provider"/> 为 null 时抛出。</exception>
        public SequenceBufferSegmentProvider(MemoryBlockProvider<T> provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// 获取一个可以承载至少 <paramref name="sizeHint"/> 个元素的序列缓冲段。
        /// </summary>
        /// <param name="sizeHint">期望的最小可写元素数，传 0 表示不限。</param>
        /// <returns>一个包装好的 <see cref="SequenceBufferSegment{T}"/> 实例。派生实现可决定是否复用或新建。 返回的段必须与其底层 <see cref="MemoryBlock{T}"/> 的生命周期策略一致（例如归还到提供者或由调用方负责释放）。</returns>
        public SequenceBufferSegment<T> GetSegment(int sizeHint)
        {
            var block = _provider.GetBuffer(sizeHint);
            var segment = GetSegment(block);
            return segment;
        }

        /// <summary>
        /// 获取指定内存块对应的序列缓冲段。
        /// </summary>
        /// <param name="block">要包装的内存块实例，不能为空。</param>
        /// <returns>与 <paramref name="block"/> 对应的 <see cref="SequenceBufferSegment{T}"/> 实例。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="block"/> 为 null 时抛出。</exception>
        public SequenceBufferSegment<T> GetSegment(MemoryBlock<T> block)
        {
            if (block is null)
                throw new ArgumentNullException(nameof(block));

            var segment = _pool.Get();
            segment.Initialize(this, block);
            return segment;
        }

        /// <summary>
        /// 释放指定的段实例（外部入口）。
        /// </summary>
        /// <param name="segment">要释放的段，不能为空。</param>
        /// <exception cref="ArgumentNullException"><paramref name="segment"/> 为 null 时抛出。</exception>
        public void ReleaseSegment(SequenceBufferSegment<T> segment)
        {
            if (segment is null)
                throw new ArgumentNullException(nameof(segment));

            _pool.Release(segment);
        }
    }
}