using System.Text;
using ExtenderApp.Buffer;

namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 跨模块烟雾测试：覆盖空缓冲、序列缓冲、对象池、值缓存与 Span 工具等。
/// </summary>
public class CrossModuleSmokeTests
{
    /// <summary>
    /// 验证 <see cref="EmptyBuffer{T}"/> 容量为零且 <see cref="EmptyBuffer{T}.ToArray"/> 为空。
    /// </summary>
    [Fact]
    public void EmptyBuffer_ShouldHaveZeroCapacityAndEmptyArray()
    {
        var empty = new EmptyBuffer<byte>();
        Assert.Equal(0, empty.Capacity);
        Assert.Equal(0, empty.Committed);
        Assert.Empty(empty.ToArray());
    }

    /// <summary>
    /// 验证 <see cref="SequenceBuffer{T}"/> 通过 GetSpan/Advance 写入后已提交长度正确。
    /// </summary>
    [Fact]
    public void SequenceBuffer_GetSpanAdvance_CommittedMatches()
    {
        var buf = SequenceBuffer<byte>.GetBuffer();
        try
        {
            var span = buf.GetSpan(4);
            span[0] = 10;
            span[1] = 20;
            span[2] = 30;
            span[3] = 40;
            buf.Advance(4);
            Assert.Equal(4, buf.Committed);
        }
        finally
        {
            buf.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="ByteBuffer"/> 写入后已提交长度正确，释放不抛异常。
    /// </summary>
    [Fact]
    public void ByteBuffer_WriteAdvance_DisposeOk()
    {
        var buffer = new ByteBuffer();
        try
        {
            var span = buffer.GetSpan(3);
            span[0] = 1;
            span[1] = 2;
            span[2] = 3;
            buffer.Advance(3);
            Assert.Equal(3, buffer.Committed);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="DefaultObjectPool{T}"/> 在默认策略下可租还 <see cref="StringBuilder"/>。
    /// </summary>
    [Fact]
    public void DefaultObjectPool_GetRelease_StringBuilder()
    {
        var pool = new DefaultObjectPool<StringBuilder>(new DefaultPooledObjectPolicy<StringBuilder>());
        var sb = pool.Get();
        sb.Append('z');
        pool.Release(sb);
        var again = pool.Get();
        Assert.NotNull(again);
    }

    /// <summary>
    /// 验证 <see cref="ValueCache.FromValue{T}(T)"/> 可取出同类型值并 <see cref="ValueCache.Release"/>。
    /// </summary>
    [Fact]
    public void ValueCache_FromValue_TryGetAndRelease()
    {
        var cache = ValueCache.FromValue(2026);
        try
        {
            Assert.True(cache.TryGetValue<int>(out var v));
            Assert.Equal(2026, v);
        }
        finally
        {
            cache.Release();
        }
    }

    /// <summary>
    /// 验证 <see cref="SpanReader{T}"/> 顺序读取与 Consumed 计数。
    /// </summary>
    [Fact]
    public void SpanReader_TryRead_AdvancesConsumed()
    {
        var reader = new SpanReader<byte>(new byte[] { 7, 8, 9 });
        Assert.True(reader.TryRead(out var a));
        Assert.Equal(7, a);
        Assert.Equal(1, reader.Consumed);
        Assert.True(reader.TryRead(out var b));
        Assert.Equal(8, b);
        Assert.False(reader.IsCompleted);
        Assert.True(reader.TryRead(out _));
        Assert.True(reader.IsCompleted);
    }

    /// <summary>
    /// 验证 <see cref="BinaryPrimitivesExtensions.WriteInt32"/> 与 <see cref="BinaryPrimitivesExtensions.ReadInt32"/> 大端往返。
    /// </summary>
    [Fact]
    public void BinaryPrimitivesExtensions_Int32BigEndian_RoundTrip()
    {
        Span<byte> span = stackalloc byte[4];
        span.WriteInt32(-12345678, isBigEndian: true);
        Assert.Equal(-12345678, ((ReadOnlySpan<byte>)span).ReadInt32(isBigEndian: true));
    }

    /// <summary>
    /// 验证两个 <see cref="ByteBlock"/> 写入相同数据后 <see cref="ByteBlock.Equals(ByteBlock)"/> 为 true（字节数组构造为零拷贝包装，不参与本场景）。
    /// </summary>
    [Fact]
    public void ByteBlock_WriteSamePayload_ShouldBeEqual()
    {
        var a = new ByteBlock(8);
        var b = new ByteBlock(8);
        try
        {
            var payload = new byte[] { 1, 2, 3 };
            a.Write(payload);
            b.Write(payload);
            Assert.True(a.Equals(b));
        }
        finally
        {
            a.Dispose();
            b.Dispose();
        }
    }
}
