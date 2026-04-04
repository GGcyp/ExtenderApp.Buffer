namespace ExtenderApp.Buffer.ValueBuffers
{
    /// <summary>
    /// 提供对 <see cref="FastSequence{T}"/> 实例的池化访问。 使用单例模式（Lazy）确保在应用程序域中共享同一提供者实例。
    /// </summary>
    /// <typeparam name="T">序列项的类型。</typeparam>
    internal class FastSequenceProvider<T>
    {
        /// <summary>
        /// 懒加载的共享实例。
        /// </summary>
        private static readonly Lazy<FastSequenceProvider<T>> _shared = new(() => new());

        /// <summary>
        /// 获取共享的 <see cref="FastSequenceProvider{T}"/> 实例。
        /// </summary>
        public static FastSequenceProvider<T> Shared => _shared.Value;

        /// <summary>
        /// 内部对象池，用于租用和回收 <see cref="FastSequence{T}"/> 实例。
        /// </summary>
        private readonly ObjectPool<FastSequence<T>> _pool;

        /// <summary>
        /// 私有构造函数，初始化对象池。
        /// </summary>
        private FastSequenceProvider()
        {
            _pool = ObjectPool.Create<FastSequence<T>>();
        }

        /// <summary>
        /// 从对象池获取一个 <see cref="FastSequence{T}"/> 实例。
        /// </summary>
        /// <returns>租用的 <see cref="FastSequence{T}"/>。</returns>
        public FastSequence<T> GetBuffer()
        {
            var buffer = _pool.Get();
            buffer.Initialize(this);
            return buffer;
        }

        /// <summary>
        /// 将 <see cref="FastSequence{T}"/> 实例归还到对象池以便重用。
        /// </summary>
        /// <param name="sequence">要归还的序列实例。</param>
        public void Release(FastSequence<T> sequence)
        {
            _pool.Release(sequence);
        }
    }
}