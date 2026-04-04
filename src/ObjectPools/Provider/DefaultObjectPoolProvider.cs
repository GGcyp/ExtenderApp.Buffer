namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 通用层,默认对象池提供器。
    /// </summary>
    public class DefaultObjectPoolProvider : ObjectPoolProvider
    {
        public static DefaultObjectPoolProvider Default = new DefaultObjectPoolProvider();

        /// <summary>
        /// 要保留在池中的最大对象数。
        /// </summary>
        public int MaximumRetained { get; set; }

        public DefaultObjectPoolProvider() : this(Environment.ProcessorCount * 2)
        {
        }

        public DefaultObjectPoolProvider(int maximumRetained)
        {
            MaximumRetained = maximumRetained;
        }

        public override ObjectPool<T> Create<T>(PooledObjectPolicy<T> policy, int maximumRetained)
        {
            maximumRetained = maximumRetained == -1 ? MaximumRetained : maximumRetained;
            if (typeof(IDisposable).IsAssignableFrom(typeof(T)))
            {
                return new DisposableObjectPool<T>(policy, maximumRetained);
            }

            return new DefaultObjectPool<T>(policy, maximumRetained);
        }
    }
}