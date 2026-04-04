using System.Runtime.InteropServices;

namespace ExtenderApp.Buffer.MemoryBlocks
{
    /// <summary>
    /// 基于固定内存片段的 <see cref="MemoryBlockProvider{T}"/> 实现。 提供将外部传入的 <see cref="Memory{T}"/> / <see cref="ReadOnlyMemory{T}"/> 包装为可复用的 <see
    /// cref="MemoryBlock{T}"/> 的能力， 适用于需要将已有内存转为 MemoryBlock 管理语义的场景。
    /// </summary>
    internal class FixedMemoryBlockProvider<T> : MemoryBlockProvider<T>
    {
        private static readonly Lazy<FixedMemoryBlockProvider<T>> _default
            = new(() => new());

        /// <summary>
        /// 默认的单例提供者实例。
        /// </summary>
        public static FixedMemoryBlockProvider<T> Default => _default.Value;

        private readonly ObjectPool<FixedMemoryBlock> _blockPool = ObjectPool.Create<FixedMemoryBlock>();

        protected override sealed MemoryBlock<T> CreateBufferProtected(int sizeHint)
        {
            return GetBuffer(new T[sizeHint]);
        }

        protected override void ReleaseProtected(MemoryBlock<T> buffer)
        {
            if (buffer is FixedMemoryBlock memoryBlock)
            {
                memoryBlock.FixedMemory = Memory<T>.Empty;
                _blockPool.Release(memoryBlock);
            }
            else
            {
                buffer.Dispose();
            }
        }

        /// <summary>
        /// 将只读内存包装为 <see cref="MemoryBlock{T}"/> 并返回。
        /// </summary>
        /// <param name="readOnlyMemory">要包装的只读内存。</param>
        /// <returns>包装后的内存块。</returns>
        public MemoryBlock<T> GetBuffer(ReadOnlyMemory<T> readOnlyMemory)
        {
            return GetBuffer(MemoryMarshal.AsMemory(readOnlyMemory));
        }

        /// <summary>
        /// 将内存包装为 <see cref="MemoryBlock{T}"/> 并返回。
        /// </summary>
        /// <param name="memory">要包装的内存。</param>
        /// <returns>包装后的内存块。</returns>
        public MemoryBlock<T> GetBuffer(Memory<T> memory)
        {
            var block = _blockPool.Get();
            block.FixedMemory = memory;
            block.Initialize(this);
            return block;
        }

        private sealed class FixedMemoryBlock : MemoryBlock<T>
        {
            /// <summary>
            /// 被包装的固定内存区域。
            /// </summary>
            public Memory<T> FixedMemory;

            /// <summary>
            /// 可用的内存区域（等同于 <see cref="FixedMemory"/>）。
            /// </summary>
            protected override sealed Memory<T> AvailableMemory => FixedMemory;

            /// <summary>
            /// 初始化内存块并将已提交长度设为固定内存的全长（整块视为已填满）。
            /// </summary>
            /// <param name="provider">调用者的提供者实例。</param>
            protected internal override sealed void Initialize(MemoryBlockProvider<T> provider)
            {
                base.Initialize(provider);
                Advance(FixedMemory.Length);
            }

            /// <summary>
            /// 固定内存块不支持扩展容量，调用此方法将抛出 <see cref="NotSupportedException"/>。
            /// </summary>
            /// <param name="sizeHint">建议的扩展大小（忽略）。</param>
            protected override sealed void EnsureCapacityProtected(int sizeHint)
            {
                throw new NotSupportedException("当前不支持扩展固定大小的内存块。");
            }
        }
    }
}
