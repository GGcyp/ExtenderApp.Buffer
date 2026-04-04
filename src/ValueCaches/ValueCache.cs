using ExtenderApp.Buffer.ValueBuffers;

namespace ExtenderApp.Buffer
{
    /// <summary>
    /// 用于临时存放多种类型值的缓存容器（链表结构）。
    /// </summary>
    public sealed class ValueCache : IEquatable<ValueCache>
    {
        /// <summary>
        /// 获取默认的缓存实例，实例来自对象池管理，使用后请调用 <see cref="Release"/> 方法回收。
        /// </summary>
        /// <returns>缓存实例。</returns>
        public static ValueCache GetCache() => ValueCacheProvider.Default.Get();

        /// <summary>
        /// 从单个值创建缓存实例。
        /// </summary>
        /// <typeparam name="T">值类型。</typeparam>
        /// <param name="value">要缓存的值。</param>
        /// <returns>缓存实例。</returns>
        public static ValueCache FromValue<T>(T value)
        {
            var buffer = ValueCacheProvider.Default.Get();
            buffer.AddValue(value);
            return buffer;
        }

        /// <summary>
        /// 从两个值创建缓存实例。
        /// </summary>
        /// <typeparam name="T1">第一个值类型。</typeparam>
        /// <typeparam name="T2">第二个值类型。</typeparam>
        /// <param name="value1">第一个值。</param>
        /// <param name="value2">第二个值。</param>
        /// <returns>缓存实例。</returns>
        public static ValueCache FromValue<T1, T2>(T1 value1, T2 value2)
        {
            var buffer = ValueCacheProvider.Default.Get();
            buffer.AddValue(value1);
            buffer.AddValue(value2);
            return buffer;
        }

        /// <summary>
        /// 从三个值创建缓存实例。
        /// </summary>
        /// <typeparam name="T1">第一个值类型。</typeparam>
        /// <typeparam name="T2">第二个值类型。</typeparam>
        /// <typeparam name="T3">第三个值类型。</typeparam>
        /// <param name="value1">第一个值。</param>
        /// <param name="value2">第二个值。</param>
        /// <param name="value3">第三个值。</param>
        /// <returns>缓存实例。</returns>
        public static ValueCache FromValue<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
        {
            var buffer = ValueCacheProvider.Default.Get();
            buffer.AddValue(value1);
            buffer.AddValue(value2);
            buffer.AddValue(value3);
            return buffer;
        }

        /// <summary>
        /// 从四个值创建缓存实例。
        /// </summary>
        /// <typeparam name="T1">第一个值类型。</typeparam>
        /// <typeparam name="T2">第二个值类型。</typeparam>
        /// <typeparam name="T3">第三个值类型。</typeparam>
        /// <typeparam name="T4">第四个值类型。</typeparam>
        /// <param name="value1">第一个值。</param>
        /// <param name="value2">第二个值。</param>
        /// <param name="value3">第三个值。</param>
        /// <param name="value4">第四个值。</param>
        /// <returns>缓存实例。</returns>
        public static ValueCache FromValue<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
        {
            var buffer = ValueCacheProvider.Default.Get();
            buffer.AddValue(value1);
            buffer.AddValue(value2);
            buffer.AddValue(value3);
            buffer.AddValue(value4);
            return buffer;
        }

        /// <summary>
        /// 从五个值创建缓存实例。
        /// </summary>
        /// <typeparam name="T1">第一个值类型。</typeparam>
        /// <typeparam name="T2">第二个值类型。</typeparam>
        /// <typeparam name="T3">第三个值类型。</typeparam>
        /// <typeparam name="T4">第四个值类型。</typeparam>
        /// <typeparam name="T5">第五个值类型。</typeparam>
        /// <param name="value1">第一个值。</param>
        /// <param name="value2">第二个值。</param>
        /// <param name="value3">第三个值。</param>
        /// <param name="value4">第四个值。</param>
        /// <param name="value5">第五个值。</param>
        /// <returns>缓存实例。</returns>
        public static ValueCache FromValue<T1, T2, T3, T4, T5>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5)
        {
            var buffer = ValueCacheProvider.Default.Get();
            buffer.AddValue(value1);
            buffer.AddValue(value2);
            buffer.AddValue(value3);
            buffer.AddValue(value4);
            buffer.AddValue(value5);
            return buffer;
        }

        /// <summary>
        /// 从六个值创建缓存实例。
        /// </summary>
        /// <typeparam name="T1">第一个值类型。</typeparam>
        /// <typeparam name="T2">第二个值类型。</typeparam>
        /// <typeparam name="T3">第三个值类型。</typeparam>
        /// <typeparam name="T4">第四个值类型。</typeparam>
        /// <typeparam name="T5">第五个值类型。</typeparam>
        /// <typeparam name="T6">第六个值类型。</typeparam>
        /// <param name="value1">第一个值。</param>
        /// <param name="value2">第二个值。</param>
        /// <param name="value3">第三个值。</param>
        /// <param name="value4">第四个值。</param>
        /// <param name="value5">第五个值。</param>
        /// <param name="value6">第六个值。</param>
        /// <returns>缓存实例。</returns>
        public static ValueCache FromValue<T1, T2, T3, T4, T5, T6>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6)
        {
            var buffer = ValueCacheProvider.Default.Get();
            buffer.AddValue(value1);
            buffer.AddValue(value2);
            buffer.AddValue(value3);
            buffer.AddValue(value4);
            buffer.AddValue(value5);
            buffer.AddValue(value6);
            return buffer;
        }

        /// <summary>
        /// 从七个值创建缓存实例。
        /// </summary>
        /// <typeparam name="T1">第一个值类型。</typeparam>
        /// <typeparam name="T2">第二个值类型。</typeparam>
        /// <typeparam name="T3">第三个值类型。</typeparam>
        /// <typeparam name="T4">第四个值类型。</typeparam>
        /// <typeparam name="T5">第五个值类型。</typeparam>
        /// <typeparam name="T6">第六个值类型。</typeparam>
        /// <typeparam name="T7">第七个值类型。</typeparam>
        /// <param name="value1">第一个值。</param>
        /// <param name="value2">第二个值。</param>
        /// <param name="value3">第三个值。</param>
        /// <param name="value4">第四个值。</param>
        /// <param name="value5">第五个值。</param>
        /// <param name="value6">第六个值。</param>
        /// <param name="value7">第七个值。</param>
        /// <returns>缓存实例。</returns>
        public static ValueCache FromValue<T1, T2, T3, T4, T5, T6, T7>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7)
        {
            var buffer = ValueCacheProvider.Default.Get();
            buffer.AddValue(value1);
            buffer.AddValue(value2);
            buffer.AddValue(value3);
            buffer.AddValue(value4);
            buffer.AddValue(value5);
            buffer.AddValue(value6);
            buffer.AddValue(value7);
            return buffer;
        }

        /// <summary>
        /// 从八个值创建缓存实例。
        /// </summary>
        /// <typeparam name="T1">第一个值类型。</typeparam>
        /// <typeparam name="T2">第二个值类型。</typeparam>
        /// <typeparam name="T3">第三个值类型。</typeparam>
        /// <typeparam name="T4">第四个值类型。</typeparam>
        /// <typeparam name="T5">第五个值类型。</typeparam>
        /// <typeparam name="T6">第六个值类型。</typeparam>
        /// <typeparam name="T7">第七个值类型。</typeparam>
        /// <typeparam name="T8">第八个值类型。</typeparam>
        /// <param name="value1">第一个值。</param>
        /// <param name="value2">第二个值。</param>
        /// <param name="value3">第三个值。</param>
        /// <param name="value4">第四个值。</param>
        /// <param name="value5">第五个值。</param>
        /// <param name="value6">第六个值。</param>
        /// <param name="value7">第七个值。</param>
        /// <param name="value8">第八个值。</param>
        /// <returns>缓存实例。</returns>
        public static ValueCache FromValue<T1, T2, T3, T4, T5, T6, T7, T8>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8)
        {
            var buffer = ValueCacheProvider.Default.Get();
            buffer.AddValue(value1);
            buffer.AddValue(value2);
            buffer.AddValue(value3);
            buffer.AddValue(value4);
            buffer.AddValue(value5);
            buffer.AddValue(value6);
            buffer.AddValue(value7);
            buffer.AddValue(value8);
            return buffer;
        }

        /// <summary>
        /// 从九个值创建缓存实例。
        /// </summary>
        /// <typeparam name="T1">第一个值类型。</typeparam>
        /// <typeparam name="T2">第二个值类型。</typeparam>
        /// <typeparam name="T3">第三个值类型。</typeparam>
        /// <typeparam name="T4">第四个值类型。</typeparam>
        /// <typeparam name="T5">第五个值类型。</typeparam>
        /// <typeparam name="T6">第六个值类型。</typeparam>
        /// <typeparam name="T7">第七个值类型。</typeparam>
        /// <typeparam name="T8">第八个值类型。</typeparam>
        /// <typeparam name="T9">第九个值类型。</typeparam>
        /// <param name="value1">第一个值。</param>
        /// <param name="value2">第二个值。</param>
        /// <param name="value3">第三个值。</param>
        /// <param name="value4">第四个值。</param>
        /// <param name="value5">第五个值。</param>
        /// <param name="value6">第六个值。</param>
        /// <param name="value7">第七个值。</param>
        /// <param name="value8">第八个值。</param>
        /// <param name="value9">第九个值。</param>
        /// <returns>缓存实例。</returns>
        public static ValueCache FromValue<T1, T2, T3, T4, T5, T6, T7, T8, T9>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9)
        {
            var buffer = ValueCacheProvider.Default.Get();
            buffer.AddValue(value1);
            buffer.AddValue(value2);
            buffer.AddValue(value3);
            buffer.AddValue(value4);
            buffer.AddValue(value5);
            buffer.AddValue(value6);
            buffer.AddValue(value7);
            buffer.AddValue(value8);
            buffer.AddValue(value9);
            return buffer;
        }

        /// <summary>
        /// 从十个值创建缓存实例。
        /// </summary>
        /// <typeparam name="T1">第一个值类型。</typeparam>
        /// <typeparam name="T2">第二个值类型。</typeparam>
        /// <typeparam name="T3">第三个值类型。</typeparam>
        /// <typeparam name="T4">第四个值类型。</typeparam>
        /// <typeparam name="T5">第五个值类型。</typeparam>
        /// <typeparam name="T6">第六个值类型。</typeparam>
        /// <typeparam name="T7">第七个值类型。</typeparam>
        /// <typeparam name="T8">第八个值类型。</typeparam>
        /// <typeparam name="T9">第九个值类型。</typeparam>
        /// <typeparam name="T10">第十个值类型。</typeparam>
        /// <param name="value1">第一个值。</param>
        /// <param name="value2">第二个值。</param>
        /// <param name="value3">第三个值。</param>
        /// <param name="value4">第四个值。</param>
        /// <param name="value5">第五个值。</param>
        /// <param name="value6">第六个值。</param>
        /// <param name="value7">第七个值。</param>
        /// <param name="value8">第八个值。</param>
        /// <param name="value9">第九个值。</param>
        /// <param name="value10">第十个值。</param>
        /// <returns>缓存实例。</returns>
        public static ValueCache FromValue<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10)
        {
            var buffer = ValueCacheProvider.Default.Get();
            buffer.AddValue(value1);
            buffer.AddValue(value2);
            buffer.AddValue(value3);
            buffer.AddValue(value4);
            buffer.AddValue(value5);
            buffer.AddValue(value6);
            buffer.AddValue(value7);
            buffer.AddValue(value8);
            buffer.AddValue(value9);
            buffer.AddValue(value10);
            return buffer;
        }

        /// <summary>
        /// 链表头节点。
        /// </summary>
        internal ValueCacheItem? First { get; private set; }

        /// <summary>
        /// 链表尾节点。
        /// </summary>
        internal ValueCacheItem? Last { get; private set; }

        /// <summary>
        /// 只能由 <see cref="ValueCacheProvider"/> 创建实例，确保对象池管理的正确性。
        /// </summary>
        internal ValueCache()
        {
        }

        /// <summary>
        /// 比较当前缓存与另一个缓存是否相等。
        /// </summary>
        /// <param name="other">要比较的另一个缓存。</param>
        /// <returns>相等返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        public bool Equals(ValueCache? other)
        {
            if (other == null)
                return false;

            var thisItem = First;
            var otherItem = other.First;
            while (thisItem != null && otherItem != null)
            {
                if (!thisItem.Equals(otherItem))
                    return false;
                thisItem = thisItem.Next;
                otherItem = otherItem.Next;
            }
            return thisItem == null && otherItem == null;
        }

        /// <summary>
        /// 尝试获取指定类型的值。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="value">输出值。</param>
        /// <returns>获取成功返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryGetValue<T>(out T value)
        {
            value = default!;
            if (First == null)
                return false;

            var item = First;
            while (item != null)
            {
                if (item is ValueBufferItem<T> valueBuffer)
                {
                    value = valueBuffer.Value;
                    return true;
                }
                else if (item is ValueBufferItem<object> objectBuffer && objectBuffer.Value is T typedValue)
                {
                    value = typedValue;
                    return true;
                }
                else if (typeof(T) == typeof(object))
                {
                    // 值类型节点（如 ValueBufferItem<int>）在以 object 为类型参数读取时进行装箱。
                    value = (T)(object)item.GetValueAsObject()!;
                    return true;
                }
                item = item.Next;
            }
            return false;
        }

        /// <summary>
        /// 获取指定类型的值并从缓存中移除对应节点。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="value">输出值。</param>
        /// <returns>获取成功返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryTakeValue<T>(out T value)
        {
            value = default!;
            if (First == null)
                return false;

            ValueCacheItem? previous = null;
            var item = First;
            while (item != null)
            {
                if (item is ValueBufferItem<T> valueBuffer)
                {
                    value = valueBuffer.Value;
                    RemoveItem(previous, item);
                    return true;
                }
                else if (item is ValueBufferItem<object> objectBuffer && objectBuffer.Value is T typedValue)
                {
                    value = typedValue;
                    RemoveItem(previous, item);
                    return true;
                }
                else if (typeof(T) == typeof(object))
                {
                    value = (T)(object)item.GetValueAsObject()!;
                    RemoveItem(previous, item);
                    return true;
                }

                previous = item;
                item = item.Next;
            }
            return false;
        }

        /// <summary>
        /// 尝试追加一个值到缓存。
        /// </summary>
        /// <typeparam name="T">值类型。</typeparam>
        /// <param name="value">要追加的值。</param>
        /// <returns>追加成功返回 <c>true</c>。</returns>
        public bool TryAddValue<T>(T value)
        {
            var type = typeof(T);
            if (type.IsValueType)
            {
                var bufferItem = ValueCacheItemProvider<T>.Default.Get();
                bufferItem.Value = value;
                Append(bufferItem);
                return true;
            }
            else
            {
                var bufferItem = ValueCacheItemProvider<object>.Default.Get();
                bufferItem.Value = value!;
                Append(bufferItem);
                return true;
            }
        }

        /// <summary>
        /// 尝试替换指定类型的值。
        /// </summary>
        /// <typeparam name="T">值类型。</typeparam>
        /// <param name="value">要替换的新值。</param>
        /// <returns>替换成功返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryChangedValue<T>(T value)
        {
            if (First == null)
                return false;
            var item = First;
            while (item != null)
            {
                if (item is ValueBufferItem<T> valueBuffer)
                {
                    valueBuffer.Value = value;
                    return true;
                }
                else if (item is ValueBufferItem<object> objectBuffer && objectBuffer.Value is T)
                {
                    objectBuffer.Value = value!;
                    return true;
                }
                item = item.Next;
            }
            return false;
        }

        /// <summary>
        /// 尝试获取指定类型的节点及其前置节点。
        /// </summary>
        /// <typeparam name="T">值类型。</typeparam>
        /// <param name="value">输出值。</param>
        /// <param name="previous">输出前置节点。</param>
        /// <param name="current">输出当前节点。</param>
        /// <returns>找到返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        internal bool TryGetNode<T>(out T value, out ValueCacheItem? previous, out ValueCacheItem? current)
        {
            value = default!;
            previous = null;
            current = null;

            if (First == null)
                return false;

            var item = First;
            while (item != null)
            {
                if (item is ValueBufferItem<T> valueBuffer)
                {
                    value = valueBuffer.Value;
                    current = item;
                    return true;
                }
                else if (item is ValueBufferItem<object> objectBuffer && objectBuffer.Value is T typedValue)
                {
                    value = typedValue;
                    current = item;
                    return true;
                }
                else if (typeof(T) == typeof(object))
                {
                    value = (T)(object)item.GetValueAsObject()!;
                    current = item;
                    return true;
                }

                previous = item;
                item = item.Next;
            }

            previous = null;
            current = null;
            return false;
        }

        /// <summary>
        /// 向缓存追加一个值。
        /// </summary>
        /// <typeparam name="T">值类型。</typeparam>
        /// <param name="value">要追加的值。</param>
        public void AddValue<T>(T value)
        {
            var type = typeof(T);
            if (type.IsValueType)
            {
                var bufferItem = ValueCacheItemProvider<T>.Default.Get();
                bufferItem.Value = value;
                Append(bufferItem);
            }
            else
            {
                var bufferItem = ValueCacheItemProvider<object>.Default.Get();
                bufferItem.Value = value!;
                Append(bufferItem);
            }
        }

        /// <summary>
        /// 检查缓存中是否包含指定类型的值。
        /// </summary>
        /// <typeparam name="T">指定类型 <see cref="{T}"/> </typeparam>
        /// <returns>包含则为 true ,否者为 false</returns>
        public bool Contains<T>()
        {
            if (First == null)
                return false;

            // 任意已缓存值均可视为 object（含值类型的装箱），故非空链表中即视为包含 object。
            if (typeof(T) == typeof(object))
                return true;

            var item = First;
            while (item != null)
            {
                if (item is ValueBufferItem<T> valueBuffer)
                {
                    return true;
                }
                else if (item is ValueBufferItem<object> objectBuffer && objectBuffer.Value is T typedValue)
                {
                    return true;
                }
                item = item.Next;
            }
            return false;
        }

        /// <summary>
        /// 追加节点到链表尾部。
        /// </summary>
        /// <typeparam name="T">节点值类型。</typeparam>
        /// <param name="item">节点实例。</param>
        private void Append<T>(ValueBufferItem<T> item)
        {
            if (Last == null)
            {
                First = item;
                Last = item;
            }
            else
            {
                Last.Next = item;
                Last = item;
            }
        }

        /// <summary>
        /// 移除指定节点并回收。
        /// </summary>
        /// <param name="previous">被移除节点的前一个节点。</param>
        /// <param name="item">要移除的节点。</param>
        private void RemoveItem(ValueCacheItem? previous, ValueCacheItem item)
        {
            if (previous == null)
            {
                First = item.Next;
            }
            else
            {
                previous.Next = item.Next;
            }

            if (Last == item)
            {
                Last = previous;
            }

            item.Release();
        }

        /// <summary>
        /// 释放并回收当前缓存实例。
        /// </summary>
        public void Release()
        {
            Clear();
            ValueCacheProvider.Default.Release(this);
        }

        /// <summary>
        /// 清空所有节点并重置链表。
        /// </summary>
        public void Clear()
        {
            var item = First;
            while (item != null)
            {
                var next = item.Next;
                item.Release();
                item = next;
            }

            First = null;
            Last = null;
        }
    }
}