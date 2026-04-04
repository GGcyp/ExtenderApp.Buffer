using System.Buffers;
using System.Text;
using ExtenderApp.Buffer.MemoryBlocks;
using ExtenderApp.Buffer.SequenceBuffers;
using ExtenderApp.Buffer.ValueBuffers;

namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 补充公开 API 覆盖：<see cref="MemoryPoolBlockProvider{T}"/>、<see cref="MemoryOwnerBlockProvider{T}"/>、
/// <see cref="ByteBufferExtensions"/>、<see cref="ObjectPool"/> / <see cref="DefaultObjectPoolProvider"/>、
/// <see cref="MemoryBlockExtensions"/>、<see cref="SequenceBufferExtensions"/>、<see cref="FastSequenceExtensions"/> 与
/// <see cref="AbstractBufferReaderExtensions.TryRead{T}"/>。
/// </summary>
public class PublicApiCoverageTests
{
    private sealed class ObjectPoolFuncTarget
    {
        public int Marker { get; set; }
    }

    private sealed class DisposableTrack : IDisposable
    {
        public int DisposeCalls { get; private set; }
        public void Dispose() => DisposeCalls++;
    }

    /// <summary>
    /// 验证 <see cref="MemoryPoolBlockProvider{T}.GetBuffer(int)"/> 可写入并在 <see cref="MemoryPoolBlockProvider{T}.Release"/> 后回收。
    /// </summary>
    [Fact]
    public void MemoryPoolBlockProvider_GetBuffer_Write_Release()
    {
        var provider = MemoryPoolBlockProvider<byte>.Default;
        var block = provider.GetBuffer(32);
        try
        {
            block.Write(new byte[] { 1, 2, 3 });
            Assert.Equal(3, block.Committed);
        }
        finally
        {
            provider.Release(block);
        }
    }

    /// <summary>
    /// 验证 <see cref="MemoryOwnerBlockProvider{T}.GetBuffer(int)"/> 获得固定容量块并可正常释放。
    /// </summary>
    [Fact]
    public void MemoryOwnerBlockProvider_GetBuffer_Write_Release()
    {
        var provider = MemoryOwnerBlockProvider<byte>.Default;
        var block = provider.GetBuffer(16);
        try
        {
            block.Write(new byte[] { 9, 8, 7 });
            Assert.Equal(3, block.Committed);
        }
        finally
        {
            provider.Release(block);
        }
    }

    /// <summary>
    /// 验证 <see cref="MemoryOwnerBlockProvider{T}.GetBuffer(IMemoryOwner{T})"/> 在传入 null 时抛出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void MemoryOwnerBlockProvider_GetBuffer_NullOwner_Throws()
    {
        var provider = MemoryOwnerBlockProvider<byte>.Default;
        Assert.Throws<ArgumentNullException>(() => provider.GetBuffer(null!));
    }

    /// <summary>
    /// 验证固定容量 <see cref="MemoryOwnerBlockProvider{T}"/> 块在写满已租用容量后再写入会触发扩容失败并抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    [Fact]
    public void MemoryOwnerBlockProvider_FixedBlock_CannotGrowPastRentedSize()
    {
        var provider = MemoryOwnerBlockProvider<byte>.Default;
        var block = provider.GetBuffer(8);
        try
        {
            var cap = (int)block.Capacity;
            block.GetSpan(cap).Fill(0xCC);
            block.Advance(cap);
            Assert.Throws<InvalidOperationException>(() => block.Write((byte)0xFF));
        }
        finally
        {
            provider.Release(block);
        }
    }

    /// <summary>
    /// 验证 <see cref="ByteBufferExtensions"/> 写入布尔、Guid、整型与 <see cref="decimal"/> 后可按 <see cref="BinaryPrimitivesExtensions"/> 读回。
    /// </summary>
    [Fact]
    public void ByteBufferExtensions_WriteBooleanGuidIntDecimal_RoundTrip()
    {
        var guid = Guid.Parse("AABBCCDD-1122-3344-5566-778899AABBCC");
        const decimal dec = 123.45m;

        var buffer = new ByteBuffer();
        buffer.WriteBoolean(false);
        buffer.WriteGuid(guid);
        buffer.Write(0xABCDEF01, isBigEndian: true);
        buffer.WriteDecimal(dec);

        var span = (ReadOnlySpan<byte>)buffer.CommittedSequence.ToArray();
        var o = 0;
        Assert.False(span.Slice(o, 1).ReadBoolean());
        o += 1;
        Assert.Equal(guid, span.Slice(o, 16).ReadGuid());
        o += 16;
        Assert.Equal(unchecked((int)0xABCDEF01), span.Slice(o, 4).ReadInt32(isBigEndian: true));
        o += 4;
        Assert.Equal(dec, span.Slice(o, 16).ReadDecimal());

        buffer.Dispose();
    }

    /// <summary>
    /// 验证 <see cref="ByteBufferExtensions.WriteDateTime"/> 与 <see cref="ByteBufferExtensions.Write"/> 字符串、字符写入后长度与解码一致。
    /// </summary>
    [Fact]
    public void ByteBufferExtensions_WriteDateTimeCharString_EncodingRoundTrip()
    {
        var dt = new DateTime(2026, 4, 4, 12, 0, 0, DateTimeKind.Utc);
        var buffer = new ByteBuffer();
        buffer.WriteDateTime(dt, isBigEndian: true);
        buffer.WriteChar('Z', Encoding.UTF8);
        buffer.Write("ok", Encoding.UTF8);

        var span = (ReadOnlySpan<byte>)buffer.CommittedSequence.ToArray();
        Assert.Equal(dt.Ticks, span.Slice(0, 8).ReadInt64(isBigEndian: true));
        var zLen = Encoding.UTF8.GetByteCount("Z");
        Assert.Equal('Z', Encoding.UTF8.GetChars(span.Slice(8, zLen).ToArray())[0]);
        Assert.Equal("ok", Encoding.UTF8.GetString(span.Slice(8 + zLen, Encoding.UTF8.GetByteCount("ok")).ToArray()));

        buffer.Dispose();
    }

    /// <summary>
    /// 验证 <see cref="ObjectPool.Create{T}(Func{T}, ObjectPoolProvider?, int)"/> 使用工厂创建并可租还。
    /// </summary>
    [Fact]
    public void ObjectPool_Create_WithFunc_GetRelease()
    {
        var pool = ObjectPool.Create(() => new ObjectPoolFuncTarget { Marker = 7 });
        var a = pool.Get();
        Assert.Equal(7, a.Marker);
        pool.Release(a);
    }

    /// <summary>
    /// 验证 <see cref="DefaultObjectPoolProvider.Create{T}"/> 对实现 <see cref="IDisposable"/> 的类型返回 <see cref="DisposableObjectPool{T}"/>。
    /// </summary>
    [Fact]
    public void DefaultObjectPoolProvider_Create_IDisposable_UsesDisposablePool()
    {
        var provider = new DefaultObjectPoolProvider(4);
        var pool = provider.Create(new DefaultPooledObjectPolicy<DisposableTrack>(), 2);
        Assert.IsType<DisposableObjectPool<DisposableTrack>>(pool);
    }

    /// <summary>
    /// 验证 <see cref="SequenceBuffer{T}"/> 经 <see cref="MemoryBlockExtensions.ToMemoryBlock"/> 得到单块且内容一致。
    /// </summary>
    [Fact]
    public void MemoryBlockExtensions_ToMemoryBlock_FromSequenceBuffer_CopiesBytes()
    {
        var seq = SequenceBuffer<byte>.GetBuffer();
        try
        {
            seq.Write(new byte[] { 10, 20, 30 });
            using var mb = seq.ToMemoryBlock();
            Assert.Equal(3, mb.Committed);
            Assert.Equal(new byte[] { 10, 20, 30 }, mb.CommittedSpan.ToArray());
        }
        finally
        {
            seq.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="MemoryBlock{T}"/> 经 <see cref="MemoryBlockExtensions.ToMemoryBlock"/> 返回同一实例引用。
    /// </summary>
    [Fact]
    public void MemoryBlockExtensions_ToMemoryBlock_FromMemoryBlock_ReturnsSameInstance()
    {
        var block = MemoryBlock<byte>.GetBuffer(new byte[] { 1, 2 });
        try
        {
            AbstractBuffer<byte> ab = block;
            var again = ab.ToMemoryBlock();
            Assert.Same(block, again);
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="SequenceBufferExtensions.ToSequenceBuffer"/> 将 <see cref="MemoryBlock{T}"/> 转为序列且内容一致。
    /// </summary>
    [Fact]
    public void SequenceBufferExtensions_ToSequenceBuffer_FromMemoryBlock_AppendsCommitted()
    {
        var block = MemoryBlock<byte>.GetBuffer(new byte[] { 5, 6 });
        try
        {
            AbstractBuffer<byte> ab = block;
            var seq = ab.ToSequenceBuffer();
            try
            {
                Assert.Equal(2, seq.Committed);
                Assert.Equal(new byte[] { 5, 6 }, ((ReadOnlySequence<byte>)seq).ToArray());
            }
            finally
            {
                seq.TryRelease();
            }
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="SequenceBufferExtensions.ToArraySegments"/> 返回非空列表且可索引到已提交段。
    /// </summary>
    [Fact]
    public void SequenceBufferExtensions_ToArraySegments_HasCommittedSegment()
    {
        var buf = SequenceBuffer<byte>.GetBuffer();
        try
        {
            buf.Write(new byte[] { 1, 2, 3 });
            var list = buf.ToArraySegments();
            Assert.NotNull(list);
            Assert.True(list!.Count >= 1);
            Assert.Equal(3, list[0].Count);
        }
        finally
        {
            buf.TryRelease();
        }
    }

    /// <summary>
    /// 验证多段 <see cref="FastSequence{T}"/> 经 <see cref="FastSequenceExtensions.ToBuffer"/> 转为可读缓冲。
    /// </summary>
    [Fact]
    public void FastSequenceExtensions_ToBuffer_MultiSegment_ReadsAllBytes()
    {
        var fs = FastSequence<byte>.GetBuffer();
        AbstractBuffer<byte>? ab = null;
        try
        {
            const int segSize = 4096;
            var first = fs.GetSpan(segSize);
            first.Fill(0x11);
            fs.Advance(segSize);

            var second = fs.GetSpan(1);
            second[0] = 0x22;
            fs.Advance(1);

            Assert.False(fs.IsSingleSegment);

            ab = fs.ToBuffer();
            Assert.Equal(segSize + 1, ab.Committed);
            var reader = ab.GetReader();
            try
            {
                Span<byte> tail = stackalloc byte[4];
                reader.Advance(segSize - 4);
                Assert.Equal(4, reader.Read(tail));
                for (var i = 0; i < 4; i++)
                    Assert.Equal(0x11, tail[i]);
                Assert.True(reader.TryRead(out var last));
                Assert.Equal(0x22, last);
            }
            finally
            {
                reader.Release();
            }
        }
        finally
        {
            ab?.TryRelease();
            fs.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferReaderExtensions.TryRead{T}"/> 在未读字节不足时返回 false 且不推进读取位置。
    /// </summary>
    [Fact]
    public void AbstractBufferReaderExtensions_TryRead_WhenTooShort_ReturnsFalseWithoutAdvancing()
    {
        var block = MemoryBlock<byte>.GetBuffer(new byte[] { 1, 2 });
        var reader = block.GetReader();
        try
        {
            Assert.False(reader.TryRead(out int _, out var size, isBigEndian: true));
            Assert.Equal(0, size);
            Assert.Equal(0, reader.Consumed);
        }
        finally
        {
            reader.Release();
        }
    }
}
