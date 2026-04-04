using System.Buffers;

namespace ExtenderApp.Buffer.MemoryBlocks
{
    /// <summary>
    /// 基于 MemoryPool 的 MemoryBlockProvider 实现。它使用 ObjectPool 来管理 MemoryBlock 实例，并通过 MemoryPool 来提供内存。
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    public sealed class MemoryPoolBlockProvider<T> : MemoryBlockProvider<T>
    {
        private static readonly Lazy<MemoryPoolBlockProvider<T>> _default = new(static () => new());
        public static MemoryPoolBlockProvider<T> Default => _default.Value;

        private readonly ObjectPool<MemoryPoolMemoryBlock> _blockPool = ObjectPool.Create<MemoryPoolMemoryBlock>();

        private readonly MemoryPool<T> _memoryPool;

        public MemoryPoolBlockProvider() : this(MemoryPool<T>.Shared)
        {
        }

        public MemoryPoolBlockProvider(MemoryPool<T> memoryPool)
        {
            _memoryPool = memoryPool ?? throw new ArgumentNullException(nameof(memoryPool));
        }

        protected override sealed MemoryBlock<T> CreateBufferProtected(int sizeHint)
        {
            var block = _blockPool.Get();
            block.MemoryPool = _memoryPool;
            var rentSize = sizeHint > 0 ? sizeHint : 16;
            block.MemoryOwner = _memoryPool.Rent(rentSize);
            block.Initialize(this);
            return block;
        }

        protected override sealed void ReleaseProtected(MemoryBlock<T> buffer)
        {
            if (buffer is MemoryPoolMemoryBlock poolBlock)
            {
                poolBlock.MemoryPool = default!;
                poolBlock.MemoryOwner?.Dispose();
                poolBlock.MemoryOwner = default!;
                _blockPool.Release(poolBlock);
            }
            else
            {
                buffer.Dispose();
            }
        }

        /// <summary>
        /// 基于 MemoryPool 的 MemoryBlock 实现。它通过 MemoryPool 来提供内存，并在释放时将内存返回给 MemoryPool。
        /// </summary>
        private sealed class MemoryPoolMemoryBlock : MemoryBlock<T>
        {
            public MemoryPool<T> MemoryPool;
            public IMemoryOwner<T> MemoryOwner;

            protected override sealed Memory<T> AvailableMemory => MemoryOwner.Memory;

            public MemoryPoolMemoryBlock()
            {
                MemoryPool = default!;
                MemoryOwner = default!;
            }

            protected override sealed void EnsureCapacityProtected(int sizeHint)
            {
                var newMemoryOwner = MemoryPool.Rent(sizeHint);
                if (MemoryOwner != null)
                {
                    MemoryOwner.Memory.CopyTo(newMemoryOwner.Memory);
                    MemoryOwner.Dispose();
                }
                MemoryOwner = newMemoryOwner;
            }
        }
    }
}