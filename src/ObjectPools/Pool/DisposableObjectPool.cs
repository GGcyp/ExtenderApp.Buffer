using System.Runtime.CompilerServices;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 默认可以被销毁对象的对象池
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class DisposableObjectPool<T> : DefaultObjectPool<T>, IDisposable where T : class
    {
        private volatile bool _isDisposed;

        public DisposableObjectPool(PooledObjectPolicy<T> policy)
            : base(policy)
        {
        }

        public DisposableObjectPool(PooledObjectPolicy<T> policy, int maximumRetained)
            : base(policy, maximumRetained)
        {
        }

        public override sealed T Get()
        {
            if (_isDisposed)
            {
                ThrowObjectDisposedException();
            }

            return base.Get();

            void ThrowObjectDisposedException()
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        public override sealed void Release(T obj)
        {
            if (_isDisposed || !ReleaseCore(obj))
            {
                DisposeItem(obj);
            }
        }

        public void Dispose()
        {
            _isDisposed = true;

            DisposeItem(_fastItem);
            _fastItem = null;

            while (_items.TryDequeue(out var item))
            {
                DisposeItem(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DisposeItem(T? item)
        {
            if (item is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}