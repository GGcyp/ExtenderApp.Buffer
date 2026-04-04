using System.Runtime.CompilerServices;

namespace ExtenderApp.Buffer.Primitives
{
    /// <summary>
    /// 表示一个支持引用计数冻结/解冻操作的对象。
    /// </summary>
    /// <remarks>
    /// 通过内部的引用计数实现可嵌套的冻结（调用 <see cref="Freeze" /> 增加计数， <see cref="Unfreeze" /> 减少计数）。 当计数大于 0 时， <see cref="IsFrozen" /> 为 true，调用 <see
    /// cref="CheckFrozen(string)" /> 将抛出 <see cref="InvalidOperationException" />。 线程安全：对计数的修改使用 <see cref="Interlocked" /> 进行原子操作。
    /// </remarks>
    public class FreezeObject : DisposableObject
    {
        private const string DefaultFrozenMessage = "当前实例已被冻结，无法修改。";

        /// <summary>
        /// 冻结引用计数。
        /// </summary>
        private long freezeCount;

        /// <summary>
        /// 获取一个值，指示当前实例是否处于冻结状态（引用计数大于 0 时视为冻结）。
        /// </summary>
        public bool IsFrozen => Interlocked.Read(ref freezeCount) > 0;

        /// <summary>
        /// 将实例设为冻结状态（引用计数递增）。
        /// </summary>
        /// <remarks>支持嵌套冻结；每次调用都会递增内部计数。该方法线程安全。</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Freeze()
        {
            Interlocked.Increment(ref freezeCount);
        }

        /// <summary>
        /// 解除一次冻结（引用计数递减）。
        /// </summary>
        /// <remarks>当内部计数递减到小于 0 时，会将计数置为 0 以防止出现负值。该方法线程安全。</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unfreeze()
        {
            var newCount = Interlocked.Decrement(ref freezeCount);
            if (newCount < 0)
            {
                Interlocked.Exchange(ref freezeCount, 0);
            }
        }

        /// <summary>
        /// 在当前实例被冻结时抛出 <see cref="InvalidOperationException" />。
        /// </summary>
        /// <param name="message">当抛出异常时使用的消息。默认值为 <see cref="DefaultFrozenMessage" />。</param>
        /// <exception cref="InvalidOperationException">当实例处于冻结状态时抛出。</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CheckFrozen(string message = DefaultFrozenMessage)
        {
            if (IsFrozen)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
