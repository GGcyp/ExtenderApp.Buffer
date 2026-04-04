namespace ExtenderApp.Buffer.ValueBuffers
{
    /// <summary>
    /// ValueCache 链表节点基类。
    /// </summary>
    internal abstract class ValueCacheItem
    {
        /// <summary>
        /// 下一节点。
        /// </summary>
        internal ValueCacheItem? Next { get; set; }

        /// <summary>
        /// 释放当前节点并清理状态。
        /// </summary>
        internal abstract void Release();

        /// <summary>
        /// 将节点内存储的值以 <see cref="object"/> 形式返回（值类型会装箱），供 <c>ValueCache</c> 在以 <c>object</c> 为类型参数时读取。
        /// </summary>
        internal abstract object? GetValueAsObject();
    }

    /// <summary>
    /// 泛型缓存节点。
    /// </summary>
    /// <typeparam name="T">节点存储的值类型。</typeparam>
    internal class ValueBufferItem<T> : ValueCacheItem, IEquatable<T>
    {
        /// <summary>
        /// 当前节点存储的值。
        /// </summary>
        public T Value { get; set; }

        /// <summary>
        /// 初始化节点实例。
        /// </summary>
        public ValueBufferItem()
        {
            Value = default!;
        }

        /// <summary>
        /// 释放当前节点并归还到对象池。
        /// </summary>
        internal override void Release()
        {
            Value = default!;
            Next = null;
            ValueCacheItemProvider<T>.Default.Release(this);
        }

        /// <inheritdoc/>
        internal override object? GetValueAsObject() => Value;

        /// <summary>
        /// 比较当前节点值与指定值是否相等。
        /// </summary>
        /// <param name="other">要比较的值。</param>
        /// <returns>相等返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        public bool Equals(T? other)
        {
            return EqualityComparer<T>.Default.Equals(Value, other);
        }

        /// <summary>
        /// 确定指定对象是否等于当前节点。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>相等返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        public override bool Equals(object? obj)
        {
            return obj is ValueBufferItem<T> other && Equals(other.Value);
        }

        /// <summary>
        /// 返回当前节点的哈希码。
        /// </summary>
        /// <returns>哈希码。</returns>
        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? 0;
        }
    }
}