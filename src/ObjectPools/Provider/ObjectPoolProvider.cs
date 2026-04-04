namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 通用层，对象池提供器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class ObjectPoolProvider
    {
        /// <summary>
        /// 生成一个 <see cref="ObjectPool"/> 实例。
        /// </summary>
        /// <typeparam name="T">用这个类型生成对象池</typeparam>
        public ObjectPool<T> Create<T>() where T : class, new()
        {
            return Create(new DefaultPooledObjectPolicy<T>());
        }

        /// <summary>
        /// 使用 <see cref="PooledObjectPolicy{T}"/> 生成 <see cref="ObjectPool"/>。
        /// </summary>
        /// <typeparam name="T">要生成对象池的类型</typeparam>
        /// <param name="policy">用于管理对象的 <see cref="PooledObjectPolicy{T}"/> 策略</param>
        /// <param name="maximumRetained">对象池中最多保留的对象数量</param>
        /// <returns>生成的 <see cref="ObjectPool{T}"/> 对象</returns>
        /// <remarks>此方法是一个抽象方法，需要子类实现。</remarks>
        public abstract ObjectPool<T> Create<T>(PooledObjectPolicy<T> policy, int maximumRetained = -1) where T : class;
    }
}