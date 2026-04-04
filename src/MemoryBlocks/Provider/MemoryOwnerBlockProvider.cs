using System.Buffers;

namespace ExtenderApp.Buffer.MemoryBlocks
{
    /// <summary>
    /// 基于固定大小的 <see cref="IMemoryOwner{T}"/> 提供内存块的提供者。 返回的内存块包装由 <see cref="MemoryPool{T}"/> 提供的 <see cref="IMemoryOwner{T}"/>，底层容量固定且不可扩容。
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    public sealed class MemoryOwnerBlockProvider<T> : MemoryBlockProvider<T>
    {
        private const int DefaultInitialBlockSize = 16;

        private static readonly Lazy<MemoryOwnerBlockProvider<T>> _default = new(static () => new());
        public static MemoryOwnerBlockProvider<T> Default => _default.Value;

        private readonly ObjectPool<MemoryOwnerMemoryBlock> _blockPool =
            ObjectPool.Create<MemoryOwnerMemoryBlock>();

        private readonly MemoryPool<T> _memoryPool;

        /// <summary>
        /// 使用默认 <see cref="MemoryPool{T}.Shared"/> 创建提供者实例。
        /// </summary>
        public MemoryOwnerBlockProvider() : this(MemoryPool<T>.Shared)
        {
        }

        /// <summary>
        /// 使用指定的 <see cref="MemoryPool{T}"/> 创建提供者实例。
        /// </summary>
        /// <param name="memoryPool">用于租用内存所有者的内存池，不能为空。</param>
        public MemoryOwnerBlockProvider(MemoryPool<T>? memoryPool)
        {
            _memoryPool = memoryPool ?? throw new ArgumentNullException(nameof(memoryPool));
        }

        /// <summary>
        /// 获取一个能够承载至少 <paramref name="sizeHint"/> 个元素的内存块（从对象池获取）。 返回的内存块包装由 <see cref="_memoryPool"/> 租用的 <see cref="IMemoryOwner{T}"/>，该块不可扩容。
        /// </summary>
        /// <param name="sizeHint">建议的最小容量（元素数）。</param>
        protected override sealed MemoryBlock<T> CreateBufferProtected(int sizeHint)
        {
            var owner = sizeHint > 0 ? _memoryPool.Rent(sizeHint) : _memoryPool.Rent(DefaultInitialBlockSize);
            return GetBuffer(owner);
        }

        /// <summary>
        /// 使用已有的 <see cref="IMemoryOwner{T}"/> 创建并返回一个包装该 owner 的内存块（不复制）。
        /// </summary>
        /// <param name="memory">要包装的 <see cref="IMemoryOwner{T}"/>（不能为空）。</param>
        /// <returns>包装指定 owner 的 <see cref="MemoryBlock{T}"/> 实例。</returns>
        public MemoryBlock<T> GetBuffer(IMemoryOwner<T> memory)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            var block = _blockPool.Get();
            block.MemoryOwner = memory;
            block.Initialize(this);
            return block;
        }

        protected override sealed void ReleaseProtected(MemoryBlock<T> buffer)
        {
            if (buffer is MemoryOwnerMemoryBlock ownerBlock)
            {
                ownerBlock.MemoryOwner?.Dispose();
                ownerBlock.MemoryOwner = default!;
                _blockPool.Release(ownerBlock);
            }
            else
            {
                buffer.Dispose();
            }
        }

        /// <summary>
        /// 内部实现：包装固定大小的 <see cref="IMemoryOwner{T}"/>，不可扩容。
        /// </summary>
        private sealed class MemoryOwnerMemoryBlock : MemoryBlock<T>
        {
            /// <summary>
            /// 底层的内存所有者（由提供者或外部传入）。
            /// </summary>
            public IMemoryOwner<T> MemoryOwner = default!;

            public MemoryOwnerMemoryBlock()
            {
                MemoryOwner = default!;
            }

            /// <summary>
            /// 返回底层可用内存（固定不变，由 MemoryOwner 提供）。
            /// </summary>
            protected override sealed Memory<T> AvailableMemory => MemoryOwner.Memory;

            /// <summary>
            /// 对于固定大小的内存块，不允许扩容；当请求的可写空间大于剩余时抛出异常。
            /// </summary>
            /// <param name="sizeHint">期望的最小可写元素数。</param>
            protected override sealed void EnsureCapacityProtected(int sizeHint)
            {
                throw new InvalidOperationException("底层为固定大小的内存，无法扩容以满足请求的写入空间。");
            }

            protected override sealed void DisposeManagedResources()
            {
                base.DisposeManagedResources();
                MemoryOwner?.Dispose();
            }
        }
    }
}