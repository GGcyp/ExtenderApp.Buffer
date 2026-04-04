namespace ExtenderApp.Buffer.MemoryBlocks
{
    /// <summary>
    /// 为 <see cref="MemoryBlock{T}"/> 提供获取与释放的抽象基类。
    /// </summary>
    /// <typeparam name="T">内存块中元素的类型。</typeparam>
    public abstract class MemoryBlockProvider<T> : AbstractBufferProvider<T, MemoryBlock<T>>
    {
        public static MemoryBlockProvider<T> Shared = ArrayPoolBlockProvider<T>.Default;
    }
}