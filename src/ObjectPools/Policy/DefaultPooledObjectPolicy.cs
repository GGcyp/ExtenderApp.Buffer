namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 通用层,基础对象池策略
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DefaultPooledObjectPolicy<T> : PooledObjectPolicy<T> where T : notnull, new()
    {
        public override T Create()
        {
            return new T();
        }

        public override bool Release(T obj)
        {
            return true;
        }
    }
}