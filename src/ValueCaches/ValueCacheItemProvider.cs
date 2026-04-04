namespace ExtenderApp.Buffer.ValueBuffers
{
    /// <summary>
    /// ValueCache 节点对象池提供器。
    /// </summary>
    internal class ValueCacheItemProvider<T>
    {
        /// <summary>
        /// 默认提供器的延迟实例。
        /// </summary>
        internal static Lazy<ValueCacheItemProvider<T>> _default
            = new(() => new());

        /// <summary>
        /// 获取默认提供器实例。
        /// </summary>
        internal static ValueCacheItemProvider<T> Default => _default.Value;

        /// <summary>
        /// 节点对象池。
        /// </summary>
        private readonly ObjectPool<ValueBufferItem<T>> _pool = ObjectPool.Create<ValueBufferItem<T>>();

        /// <summary>
        /// 从对象池获取节点。
        /// </summary>
        /// <returns>节点实例。</returns>
        public ValueBufferItem<T> Get()
        {
            return _pool.Get();
        }

        /// <summary>
        /// 归还节点到对象池。
        /// </summary>
        /// <param name="item">要归还的节点。</param>
        public void Release(ValueBufferItem<T> item)
        {
            _pool.Release(item);
        }
    }
}