using System.Runtime.CompilerServices;

namespace ExtenderApp.Buffer.Primitives
{
    /// <summary>
    /// 实现 <see cref="IDisposable"/> 与 <see cref="IAsyncDisposable"/> 的抽象基类，用于统一托管/非托管资源的释放模式。
    /// </summary>
    public abstract class DisposableObject : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// 释放状态标记（0 未释放，1 已释放）。
        /// </summary>
        [NonSerialized]
        private volatile int disposed;

        /// <summary>
        /// 获取当前实例是否已完成释放。
        /// </summary>
        public bool IsDisposed => disposed == 1;

        /// <summary>
        /// 终结器：在未被正确释放时仍尝试回收非托管资源。
        /// </summary>
        ~DisposableObject()
        {
            Dispose(false);
        }

        /// <summary>
        /// 若已释放则抛出 <see cref="ObjectDisposedException"/>。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>
        /// 同步释放全部资源（托管与非托管）。
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            try
            {
                Dispose(true);
            }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// 按是否由用户代码调用区分释放路径。
        /// </summary>
        /// <param name="disposing">为 <c>true</c> 时表示由 <see cref="Dispose()"/> 调用，可释放托管资源。</param>
        protected virtual void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                    DisposeManagedResources();
            }
            finally
            {
                DisposeUnmanagedResources();
            }
        }

        /// <summary>
        /// 异步释放资源：先执行异步托管释放，再同步完成非托管释放。
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 异步释放入口：默认委托给 <see cref="DisposeAsyncManagedResources"/>。
        /// </summary>
        protected async ValueTask DisposeAsyncCore()
        {
            await DisposeAsyncManagedResources().ConfigureAwait(false);
        }

        /// <summary>
        /// 派生类可重写以异步释放托管资源。
        /// </summary>
        protected virtual ValueTask DisposeAsyncManagedResources()
        {
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 派生类可重写以同步释放托管资源。
        /// </summary>
        protected virtual void DisposeManagedResources()
        {
        }

        /// <summary>
        /// 派生类可重写以释放非托管资源。
        /// </summary>
        protected virtual void DisposeUnmanagedResources()
        {
        }
    }
}
