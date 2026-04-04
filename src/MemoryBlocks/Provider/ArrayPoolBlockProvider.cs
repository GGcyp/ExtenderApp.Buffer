using System.Buffers;

namespace ExtenderApp.Buffer.MemoryBlocks
{
    /// <summary>
    /// 基于数组池的内存块提供者，用于创建或复用 <see cref="MemoryBlock{T}"/> 实例。
    /// </summary>
    /// <typeparam name="T">内存块中元素的类型。</typeparam>
    internal sealed class ArrayPoolBlockProvider<T> : MemoryBlockProvider<T>
    {
        private static readonly Lazy<ArrayPoolBlockProvider<T>> _default = new(static () => new());
        public static ArrayPoolBlockProvider<T> Default = _default.Value;

        private readonly ObjectPool<ArrayPoolMemoryBlock> _blockPool = ObjectPool.Create<ArrayPoolMemoryBlock>();

        private readonly ArrayPool<T> _arrayPool;

        /// <summary>
        /// 使用共享的 <see cref="ArrayPool{T}"/> 创建 <see cref="ArrayPoolBlockProvider{T}"/> 实例。
        /// </summary>
        public ArrayPoolBlockProvider() : this(ArrayPool<T>.Shared)
        {
        }

        /// <summary>
        /// 使用指定的 <see cref="ArrayPool{T}"/> 创建 <see cref="ArrayPoolBlockProvider{T}"/> 实例。
        /// </summary>
        /// <param name="arrayPool">用于租用/归还数组的数组池，不能为空。</param>
        public ArrayPoolBlockProvider(ArrayPool<T> arrayPool)
        {
            _arrayPool = arrayPool;
        }

        /// <summary>
        /// 从内部对象池获取一个可写的 <see cref="MemoryBlock{T}"/> 实例并初始化为可从数组池租用。
        /// </summary>
        /// <param name="sizeHint">建议的最小容量（元素数）。实现可以忽略或用于选择合适大小的数组。</param>
        /// <returns>一个来自对象池且已配置为从数组池租用底层数组的 <see cref="MemoryBlock{T}"/> 实例。</returns>
        protected override sealed MemoryBlock<T> CreateBufferProtected(int sizeHint)
        {
            var block = _blockPool.Get();
            block.ArrayPool = _arrayPool;
            block.Initialize(this);
            block.GetMemory(sizeHint); // 预先分配底层数组以满足初始容量提示
            return block;
        }

        protected override sealed void ReleaseProtected(MemoryBlock<T> buffer)
        {
            if (buffer is ArrayPoolMemoryBlock memoryBlock)
            {
                if (memoryBlock.TArray != null) _arrayPool.Return(memoryBlock.TArray);
                memoryBlock.TArray = default!;
                memoryBlock.LogicalLength = 0;
                memoryBlock.ArrayPool = default!;
                _blockPool.Release(memoryBlock);
            }
            else
            {
                buffer.Dispose();
            }
        }

        /// <summary>
        /// 获取一个新的 <see cref="MemoryBlock{T}"/>，并将指定的可写跨度中的数据写入该内存块。
        /// </summary>
        /// <param name="span">要写入内存块的数据可写跨度。</param>
        /// <returns>返回已初始化并包含写入数据的 <see cref="MemoryBlock{T}"/> 实例。调用方负责在不再使用时释放或归还该内存块。</returns>
        public MemoryBlock<T> GetBuffer(Span<T> span)
        {
            var block = _blockPool.Get();
            block.ArrayPool = _arrayPool;
            block.Initialize(this);
            block.Write(span);
            return block;
        }

        /// <summary>
        /// 获取一个新的 <see cref="MemoryBlock{T}"/>，并将指定的只读跨度中的数据写入该内存块。
        /// </summary>
        /// <param name="span">要写入内存块的数据只读跨度。</param>
        /// <returns>返回已初始化并包含写入数据的 <see cref="MemoryBlock{T}"/> 实例。调用方负责在不再使用时释放或归还该内存块。</returns>
        public MemoryBlock<T> GetBuffer(ReadOnlySpan<T> span)
        {
            var block = _blockPool.Get();
            block.ArrayPool = _arrayPool;
            block.Initialize(this);
            block.Write(span);
            return block;
        }

        /// <summary>
        /// 获取一个新的 <see cref="MemoryBlock{T}"/>，并将指定的内存放入该内存块。 并且当前内存块的底层数组将从数组池租用以容纳该内存。
        /// </summary>
        /// <param name="array">要写入内存块的数组。</param>
        /// <param name="committed">数组中已提交的数据长度。</param>
        /// <returns>返回已初始化并包含写入数据的 <see cref="MemoryBlock{T}"/> 实例。调用方负责在不再使用时释放或归还该内存块。</returns>
        internal MemoryBlock<T> GetBuffer(T[] array, int committed)
        {
            var block = _blockPool.Get();
            block.ArrayPool = _arrayPool;
            block.TArray = array;
            block.LogicalLength = array.Length;
            block.Initialize(this);
            block.Advance(committed);
            return block;
        }

        /// <summary>
        /// 数组池内存块的具体实现：它维护一个来自数组池的底层数组，并在释放时根据是否使用池进行适当的清理和归还。
        /// </summary>
        private sealed class ArrayPoolMemoryBlock : MemoryBlock<T>
        {
            // 以下字段由提供者/池初始化或在释放时清理
            public ArrayPool<T> ArrayPool;

            public T[] TArray;

            /// <summary>
            /// 对外可见的逻辑容量（元素数）。首次从池租用的数组实际长度可能更大，此处与 <see cref="MemoryBlock{T}.GetBuffer(int)"/> 的容量提示对齐，避免 <see cref="Available"/> 虚高。
            /// </summary>
            public int LogicalLength;

            /// <summary>
            /// 返回当前内存块的底层可用内存（包含已提交与未提交部分），长度为 <see cref="LogicalLength"/>。
            /// </summary>
            protected override sealed Memory<T> AvailableMemory =>
                TArray is null ? default : new Memory<T>(TArray, 0, LogicalLength);

            /// <summary>
            /// 构造函数：对象从池中获取后，字段由提供者初始化。
            /// </summary>
            public ArrayPoolMemoryBlock()
            {
                ArrayPool = default!;
                TArray = default!;
            }

            protected override sealed void EnsureCapacityProtected(int sizeHint)
            {
                if (TArray == null)
                {
                    TArray = ArrayPool.Rent(sizeHint);
                    LogicalLength = sizeHint;
                    return;
                }

                var newBuffer = ArrayPool.Rent((int)(Committed + sizeHint));
                TArray.AsSpan(0, (int)Committed).CopyTo(newBuffer);
                ArrayPool.Return(TArray);
                TArray = newBuffer;
                LogicalLength = newBuffer.Length;
            }
        }
    }
}