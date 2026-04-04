namespace ExtenderApp.Buffer.ValueBuffers
{
    /// <summary>
    /// ValueCache 对象池提供者。
    /// </summary>
    internal sealed class ValueCacheProvider
    {
        /// <summary>
        /// 默认提供器的延迟实例。
        /// </summary>
        internal static Lazy<ValueCacheProvider> _default
            = new(() => new());

        /// <summary>
        /// 获取默认提供器实例。
        /// </summary>
        internal static ValueCacheProvider Default => _default.Value;

        private readonly ObjectPool<ValueCache> _pool = ObjectPool.Create(static () => new ValueCache());

        /// <summary>
        /// 从对象池获取 ValueCache 实例。
        /// </summary>
        /// <returns>ValueCache 实例。</returns>
        public ValueCache Get()
        {
            return _pool.Get();
        }

        /// <summary>
        /// 归还 ValueCache 到对象池。
        /// </summary>
        /// <param name="buffer">要归还的实例。</param>
        public void Release(ValueCache buffer)
        {
            _pool.Release(buffer);
        }
    }
}