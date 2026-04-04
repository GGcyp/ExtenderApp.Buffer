namespace ExtenderApp.Buffer
{
    public class FactoryPooledObjectPolicy<T> : PooledObjectPolicy<T> where T : notnull
    {
        private readonly Func<T> _factory;

        public FactoryPooledObjectPolicy(Func<T> factory)
        {
            _factory = factory;
        }

        public override T Create()
        {
            return _factory.Invoke();
        }

        public override bool Release(T obj)
        {
            return true;
        }
    }
}