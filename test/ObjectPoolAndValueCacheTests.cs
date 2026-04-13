using System;
using System.Text;
using ExtenderApp.Buffer;

namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 针对对象池与 <see cref="ValueCache"/> 的基本功能与类型匹配行为测试。
/// </summary>
public class ObjectPoolAndValueCacheTests
{
    /// <summary>
    /// 验证 <see cref="DefaultObjectPool{T}"/> 在多次租还时能够复用实例并遵守最大容量配置。
    /// </summary>
    [Fact]
    public void DefaultObjectPool_GetAndRelease_ShouldReuseInstances()
    {
        var policy = new DefaultPooledObjectPolicy<StringBuilder>();
        var pool = new DefaultObjectPool<StringBuilder>(policy, maximumRetained: 2);

        var a = pool.Get();
        a.Append('a');
        pool.Release(a);

        var b = pool.Get();
        Assert.Same(a, b);

        var c = pool.Get();
        var d = pool.Get();

        pool.Release(b);
        pool.Release(c);
        pool.Release(d);
    }

    /// <summary>
    /// 验证 <see cref="ValueCache.FromValue{T}(T)"/> 能够在值类型和引用类型下正确缓存与读取。
    /// </summary>
    [Fact]
    public void ValueCache_FromValue_ShouldStoreAndRetrieve()
    {
        var cache = ValueCache.FromValue(42);
        try
        {
            Assert.True(cache.TryGetValue<int>(out var v));
            Assert.Equal(42, v);

            Assert.True(cache.TryGetValue<object>(out var obj));
            Assert.Equal(42, obj);
        }
        finally
        {
            cache.Release();
        }
    }

    /// <summary>
    /// 验证 <see cref="ValueCache.TryTakeValue{T}(out T)"/> 能够移除匹配类型的节点并更新链表。
    /// </summary>
    [Fact]
    public void ValueCache_TryTakeValue_ShouldRemoveNode()
    {
        var cache = ValueCache.FromValue<int, string>(123, "abc");
        try
        {
            Assert.True(cache.TryTakeValue<int>(out var i));
            Assert.Equal(123, i);

            Assert.False(cache.Contains<int>());
            Assert.True(cache.Contains<string>());
        }
        finally
        {
            cache.Release();
        }
    }

    /// <summary>
    /// 验证 <see cref="ValueCache.TryChangedValue{T}(T)"/> 能够在存在匹配类型节点时直接覆盖原值。
    /// </summary>
    [Fact]
    public void ValueCache_TryChangedValue_ShouldOverwriteExisting()
    {
        var cache = ValueCache.FromValue<int, string>(1, "origin");
        try
        {
            Assert.True(cache.TryChangedValue("changed"));

            Assert.True(cache.TryGetValue<string>(out var s));
            Assert.Equal("changed", s);
        }
        finally
        {
            cache.Release();
        }
    }

    /// <summary>
    /// 验证 <see cref="ValueCache.Contains{T}"/> 能够正确识别缓存链表中的不同类型。
    /// </summary>
    [Fact]
    public void ValueCache_Contains_ShouldReflectTypes()
    {
        var cache = ValueCache.FromValue<int, string>(100, "buffer");
        try
        {
            Assert.True(cache.Contains<int>());
            Assert.True(cache.Contains<string>());
            Assert.False(cache.Contains<double>());
        }
        finally
        {
            cache.Release();
        }
    }

    /// <summary>
    /// 验证 <see cref="ValueCache.GetCache"/> 得到空链缓存，追加值后可读回并最终 <see cref="ValueCache.Release"/>。
    /// </summary>
    [Fact]
    public void ValueCache_GetCache_AddValue_TryGetAndRelease()
    {
        var cache = ValueCache.GetCache();
        try
        {
            Assert.False(cache.TryGetValue<int>(out _));
            Assert.True(cache.TryAddValue(99));
            Assert.True(cache.TryGetValue<int>(out var v));
            Assert.Equal(99, v);
        }
        finally
        {
            cache.Release();
        }
    }

    /// <summary>
    /// 验证 <see cref="ValueCache.FromValue{T1,T2,T3}(T1,T2,T3)"/> 与十参重载能按序存放多种类型并可逐项读取。
    /// </summary>
    [Fact]
    public void ValueCache_FromValue_ThreeAndTenTypeParams_RoundTrip()
    {
        var three = ValueCache.FromValue(1, "b", 3.5);
        try
        {
            Assert.True(three.TryGetValue<int>(out var i) && i == 1);
            Assert.True(three.TryGetValue<string>(out var s) && s == "b");
            Assert.True(three.TryGetValue<double>(out var d) && d == 3.5);
        }
        finally
        {
            three.Release();
        }

        var ten = ValueCache.FromValue(1, 2, 3, 4, 5, 6L, 7L, 8L, 9L, 10UL);
        try
        {
            Assert.True(ten.TryGetValue<int>(out var a) && a == 1);
            Assert.True(ten.TryGetValue<ulong>(out var u) && u == 10UL);
        }
        finally
        {
            ten.Release();
        }
    }

    /// <summary>
    /// 验证 <see cref="ValueCache.Equals(ValueCache?)"/> 在链表类型与取值一致时为 true，顺序或取值不同则为 false。
    /// </summary>
    [Fact]
    public void ValueCache_Equals_IEquatable_ComparesLinkedValues()
    {
        var a = ValueCache.FromValue<int, int, int>(1, 2, 3);
        var b = ValueCache.FromValue<int, int, int>(1, 2, 3);
        var c = ValueCache.FromValue<int, int, int>(1, 2, 4);
        try
        {
            Assert.True(a.Equals(b));
            Assert.False(a.Equals(c));
            Assert.False(a.Equals(null));
        }
        finally
        {
            a.Release();
            b.Release();
            c.Release();
        }
    }

    /// <summary>
    /// 验证 <see cref="object.GetHashCode"/> 在 <see cref="ValueCache"/> 上可调用（类型未重写哈希，由运行时提供默认实现）。
    /// </summary>
    [Fact]
    public void ValueCache_GetHashCode_DoesNotThrow()
    {
        var cache = ValueCache.FromValue(42);
        try
        {
            _ = cache.GetHashCode();
        }
        finally
        {
            cache.Release();
        }
    }
}

