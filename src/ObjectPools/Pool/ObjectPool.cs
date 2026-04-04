using System.Collections.Concurrent;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 一个对象池基类。
    /// </summary>
    public abstract class ObjectPool<T> : ObjectPool
    {
        /// <summary>
        /// 对象池中还有多少对象
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// 从池中获取一个对象（如果有），否则创建一个对象。
        /// </summary>
        /// <returns><typeparamref name="T"/></returns>
        public abstract T Get();

        /// <summary>
        /// 将对象返回到池中。
        /// </summary>
        public abstract void Release(T obj);
    }

    /// <summary>
    /// 创建一个 <see cref="ObjectPool{T}"/> 实例。
    /// </summary>
    public abstract class ObjectPool
    {
        private static ConcurrentDictionary<Type, ObjectPool> PoolDict { get; } = new();

        /// <summary>
        /// 创建一个默认的对象池。
        /// </summary>
        /// <typeparam name="T">对象池中存储的对象的类型，必须是一个引用类型且拥有一个无参构造函数。</typeparam>
        /// <returns>返回创建的对象池。</returns>
        public static ObjectPool<T> Create<T>() where T : class, new()
        {
            if (PoolDict.TryGetValue(typeof(T), out var pool))
            {
                return (ObjectPool<T>)pool;
            }

            return Create(new DefaultPooledObjectPolicy<T>());
        }

        /// <summary>
        /// 通过一个工厂方法创建一个对象池。
        /// </summary>
        /// <typeparam name="T">对象池中的对象类型</typeparam>
        /// <param name="func">创建对象的工厂方法</param>
        /// <returns>返回创建的对象池</returns>
        public static ObjectPool<T> Create<T>(Func<T> func, ObjectPoolProvider? objectPoolProvider = null, int maximumRetained = -1) where T : class
        {
            Type poolType = typeof(T);
            if (PoolDict.TryGetValue(poolType, out var pool))
            {
                return (ObjectPool<T>)pool;
            }

            lock (PoolDict)
            {
                if (PoolDict.TryGetValue(poolType, out pool))
                {
                    return (ObjectPool<T>)pool;
                }

                var provider = objectPoolProvider ?? DefaultObjectPoolProvider.Default;
                var objPool = provider.Create(new FactoryPooledObjectPolicy<T>(func), maximumRetained);
                PoolDict.TryAdd(poolType, objPool);
                return objPool;
            }
        }

        /// <summary>
        /// 创建一个对象池
        /// </summary>
        /// <typeparam name="T">对象池中的对象类型</typeparam>
        /// <param name="policy">对象的创建和重置策略</param>
        /// <param name="objectPoolProvider">对象池提供者，默认为null</param>
        /// <param name="maximumRetained">对象池中最大保留对象数，-1表示无限制</param>
        /// <param name="canReuse">是否可以重用对象池，默认为true</param>
        /// <returns>返回创建的对象池</returns>
        public static ObjectPool<T> Create<T>(PooledObjectPolicy<T> policy, ObjectPoolProvider? objectPoolProvider = null, int maximumRetained = -1) where T : class, new()
        {
            Type poolType = typeof(T);
            if (PoolDict.TryGetValue(poolType, out var pool))
            {
                return (ObjectPool<T>)pool;
            }

            lock (PoolDict)
            {
                if (PoolDict.TryGetValue(poolType, out pool))
                {
                    return (ObjectPool<T>)pool;
                }

                var provider = objectPoolProvider ?? DefaultObjectPoolProvider.Default;
                var objPool = provider.Create(policy, maximumRetained);
                PoolDict.TryAdd(poolType, objPool);
                return objPool;
            }
        }

        //public abstract object GetBlock();

        //public abstract void ReleaseSegment(object obj);
    }
}