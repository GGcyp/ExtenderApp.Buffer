using System.Diagnostics;
using ExtenderApp.Buffer.Primitives;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 抽象缓冲区提供者：按需创建或提供泛型缓冲实例。
    /// </summary>
    /// <typeparam name="T">缓冲中元素的类型。</typeparam>
    /// <typeparam name="TBuffer">缓冲实例类型，必须继承自 <see cref="AbstractBuffer{T}"/>。</typeparam>
    public abstract class AbstractBufferProvider<T, TBuffer> : DisposableObject
        where TBuffer : AbstractBuffer<T>
    {
        private const int DefaultInitialBufferSize = 16;

        /// <summary>
        /// 获取一个可用于写入的缓冲实例。
        /// </summary>
        /// <param name="sizeHint">期望的最小容量（以元素数计）。传 0 表示不作特殊建议。</param>
        /// <returns>一个满足或尽量满足 <paramref name="sizeHint"/> 要求的 <typeparamref name="TBuffer"/> 实例。 实现可以返回新建实例或池中复用的实例，调用者应按实现约定负责后续的释放/回收操作。</returns>
        public TBuffer GetBuffer(int sizeHint = DefaultInitialBufferSize)
        {
            return CreateBufferProtected(sizeHint);
        }

        /// <summary>
        /// 派生类实现：创建或提供一个满足 <paramref name="sizeHint"/> 要求的缓冲实例。
        /// </summary>
        /// <param name="sizeHint">期望的最小容量（以元素数计）。传 0 表示不作特殊建议。</param>
        /// <returns>一个 <typeparamref name="TBuffer"/> 实例，调用者将使用该实例进行写入并负责遵循库中关于生命周期的约定。</returns>
        protected abstract TBuffer CreateBufferProtected(int sizeHint);

        /// <summary>
        /// 释放/归还一个由 <see cref="GetBuffer"/> 获取到的缓冲实例。
        /// </summary>
        /// <param name="buffer">要释放或归还的 <typeparamref name="TBuffer"/> 实例，不能为空。</param>
        /// <remarks>实现应根据提供者的策略（例如对象池或直接销毁）回收该实例，并确保在释放后该实例不再被外部使用。 调用者必须遵循生命周期约定：在释放后不得继续使用该缓冲实例，且在并发场景下需自行保证同步。</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> 为 <c>null</c> 时抛出。</exception>
        public void Release(TBuffer buffer)
        {
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));
            ReleaseProtected(buffer);
        }

        /// <summary>
        /// 派生类实现：执行具体的释放/回收逻辑（例如将实例归还到对象池或释放底层资源）。
        /// </summary>
        /// <param name="buffer">要回收的缓冲实例，调用方已保证不为 <c>null</c>。</param>
        /// <remarks>
        /// - 派生实现应保证该方法为幂等的：对同一实例重复调用不应导致不一致状态或异常（在合理范围内）。
        /// - 若实现使用对象池，应在此处重置缓冲状态以便后续重用（例如调用 <see cref="AbstractBuffer{T}.Clear"/>）。
        /// - 此方法通常由 <see cref="Release(TBuffer)"/> 调用；实现不需要再次进行空检查（但可以选择性验证）。
        /// </remarks>
        protected abstract void ReleaseProtected(TBuffer buffer);
    }
}