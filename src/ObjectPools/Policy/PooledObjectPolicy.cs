namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 表示一个对象池策略，用于创建和释放对象。
    /// </summary>
    /// <typeparam name="T">对象的类型。</typeparam>
    public abstract class PooledObjectPolicy<T> where T : notnull
    {
        /// <summary>
        /// 创建一个新的对象实例。
        /// </summary>
        /// <returns>返回新创建的对象实例。</returns>
        public abstract T Create();

        /// <summary>
        /// 释放一个对象实例。
        /// </summary>
        /// <param name="obj">要释放的对象实例。</param>
        /// <returns>如果对象成功释放，则返回 true；否则返回 false。</returns>
        public abstract bool Release(T obj);
    }
}