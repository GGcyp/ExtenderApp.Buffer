using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using ExtenderApp.Buffer.MemoryBlocks;

namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 针对 <see cref="AbstractBufferExtensions"/> 的 Write / TryWrite / Read / TryRead、字符串写入与切片扩展的分组契约测试。
/// </summary>
public class AbstractBufferExtensionsTests
{
    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.Write{T}(AbstractBuffer{byte},T,bool)"/> 写入后需由调用方 <see cref="AbstractBuffer{T}.Advance"/>，且可按大端读回。
    /// </summary>
    [Fact]
    public void AbstractBuffer_WriteUnmanaged_AdvanceThenReadBack()
    {
        var block = MemoryBlock<byte>.GetBuffer(16);
        try
        {
            block.Write(unchecked((short)0x0102), isBigEndian: true);
            block.Advance(sizeof(short));

            Assert.Equal(2, block.Committed);
            var v = block.Read<short>(isBigEndian: true);
            Assert.Equal(unchecked((short)0x0102), v);
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// 验证对 <c>null</c> 缓冲区调用 <see cref="AbstractBufferExtensions.Write{T}(AbstractBuffer{byte},T,bool)"/> 抛出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void AbstractBuffer_Write_NullBuffer_Throws()
    {
        AbstractBuffer<byte>? buffer = null;
        Assert.Throws<ArgumentNullException>(() => AbstractBufferExtensions.Write(buffer!, (byte)1));
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.TryWrite{T}(AbstractBuffer{byte},T,bool)"/> 在缓冲区为 <c>null</c> 时返回 <c>false</c>。
    /// </summary>
    [Fact]
    public void AbstractBuffer_TryWrite_NullBuffer_ReturnsFalse()
    {
        AbstractBuffer<byte>? buffer = null;
        Assert.False(buffer!.TryWrite((int)1));
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.TryWrite{T}(Span{byte},T,out int,bool)"/> 在跨度不足时返回 <c>false</c> 且 <paramref name="size"/> 为 0。
    /// </summary>
    [Fact]
    public void Span_TryWrite_InsufficientSpace_ReturnsFalse()
    {
        Span<byte> one = stackalloc byte[1];
        Assert.False(one.TryWrite(123, out var size));
        Assert.Equal(0, size);
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.Write{T}(Span{byte},T,out int,bool)"/> 在空跨度上抛出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void Span_Write_EmptySpan_ThrowsArgumentNull()
    {
        var arr = Array.Empty<byte>();
        Assert.Throws<ArgumentNullException>(() => arr.AsSpan().Write((byte)1, out _));
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.Write(Span{byte},string,Encoding,out int)"/> 在编码为 <c>null</c> 时抛出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void Span_WriteString_NullEncoding_Throws()
    {
        var arr = new byte[16];
        Assert.Throws<ArgumentNullException>(() => arr.AsSpan().Write("x", null!, out _));
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.Write(Span{byte},string,Encoding,out int)"/> 在跨度不足以容纳编码结果时抛出 <see cref="IndexOutOfRangeException"/>。
    /// </summary>
    [Fact]
    public void Span_WriteString_BufferTooSmall_Throws()
    {
        var arr = new byte[1];
        Assert.Throws<IndexOutOfRangeException>(() => arr.AsSpan().Write("你好", Encoding.UTF8, out _));
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.Write(AbstractBuffer{byte},string,Encoding)"/> 写入后已提交长度与 UTF-8 字节一致。
    /// </summary>
    [Fact]
    public void AbstractBuffer_WriteString_WithEncoding_AdvancesCommitted()
    {
        var block = MemoryBlock<byte>.GetBuffer(32);
        try
        {
            block.Write("ab", Encoding.ASCII);
            Assert.Equal(2, block.Committed);
            Assert.Equal((byte)'a', block.CommittedSpan[0]);
            Assert.Equal((byte)'b', block.CommittedSpan[1]);
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.Read{T}(ReadOnlySpan{byte},out int,bool)"/> 与 <see cref="AbstractBufferExtensions.TryRead{T}(ReadOnlySpan{byte},out T,out int,bool)"/> 在数据足够时结果一致。
    /// </summary>
    [Fact]
    public void ReadOnlySpan_ReadAndTryRead_Int32_BigEndian_Match()
    {
        Span<byte> buf = stackalloc byte[4];
        buf.Write(unchecked((int)0x01020304), out _, isBigEndian: true);

        var a = ((ReadOnlySpan<byte>)buf).Read<int>(out var sa, isBigEndian: true);
        Assert.True(((ReadOnlySpan<byte>)buf).TryRead<int>(out var b, out var sb, isBigEndian: true));
        Assert.Equal(a, b);
        Assert.Equal(sa, sb);
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.Read{T}(ReadOnlySequence{byte},out int,bool)"/> 在多段序列上拷贝并解析非托管值。
    /// </summary>
    [Fact]
    public void ReadOnlySequence_Read_Int32AcrossTwoSegments()
    {
        var first = new TestChunk(new byte[] { 0x01, 0x02 });
        var last = first.LinkTo(new TestChunk(new byte[] { 0x03, 0x04 }));
        var seq = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

        var v = seq.Read<int>(isBigEndian: true);
        Assert.Equal(unchecked((int)0x01020304), v);
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.TryRead{T}(ReadOnlySequence{byte},out T,out int,bool)"/> 在序列过短时返回 <c>false</c>。
    /// </summary>
    [Fact]
    public void ReadOnlySequence_TryRead_TooShort_ReturnsFalse()
    {
        var seq = new ReadOnlySequence<byte>(new byte[] { 1, 2 });
        Assert.False(seq.TryRead<int>(out _, out var size));
        Assert.Equal(0, size);
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.Read{T}(AbstractBuffer{byte},bool)"/> 基于已提交数据解析。
    /// </summary>
    [Fact]
    public void AbstractBuffer_Read_FromCommitted()
    {
        var block = MemoryBlock<byte>.GetBuffer(8);
        try
        {
            block.Write(new byte[] { 0, 0, 0, 0x2A });
            var v = block.Read<int>(isBigEndian: true);
            Assert.Equal(42, v);
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.Read{T}(AbstractBuffer{byte},bool)"/> 在缓冲区为 <c>null</c> 时抛出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void AbstractBuffer_Read_NullBuffer_Throws()
    {
        AbstractBuffer<byte>? buffer = null;
        Assert.Throws<ArgumentNullException>(() => buffer!.Read<int>());
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.TryRead{T}(AbstractBuffer{byte},out T,out int,bool)"/> 在缓冲区为 <c>null</c> 时抛出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void AbstractBuffer_TryRead_NullBuffer_Throws()
    {
        AbstractBuffer<byte>? buffer = null;
        Assert.Throws<ArgumentNullException>(() => buffer!.TryRead(out int _, out _));
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.Memory{T}.Write{T}(T,out int,bool)"/> 与 <see cref="AbstractBufferExtensions.Memory{T}.Read{T}(out int,bool)"/> 往返一致。
    /// </summary>
    [Fact]
    public void Memory_WriteOutSize_Read_RoundTrip()
    {
        var arr = new byte[8];
        var mem = arr.AsMemory();
        mem.Write((long)0x1122334455667788, out var w, isBigEndian: true);
        Assert.Equal(sizeof(long), w);

        var slice = mem.Slice(0, w);
        var v = slice.Read<long>(out var r, isBigEndian: true);
        Assert.Equal(sizeof(long), r);
        Assert.Equal(unchecked((long)0x1122334455667788), v);
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.CommittedSlice{T}"/> 覆盖已提交范围且长度与 <see cref="AbstractBuffer{T}.Committed"/> 一致。
    /// </summary>
    [Fact]
    public void AbstractBuffer_CommittedSlice_MatchesCommittedRange()
    {
        var block = MemoryBlock<byte>.GetBuffer(10);
        try
        {
            block.Write(new byte[] { 1, 2, 3 });
            var committed = block.CommittedSlice();
            Assert.Equal(3, committed.Committed);
            Assert.Equal(new byte[] { 1, 2, 3 }, committed.CommittedSpan.ToArray());
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferExtensions.AvailableSlice{T}"/> 在缓冲区为 <c>null</c> 时抛出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void AbstractBuffer_AvailableSlice_Null_Throws()
    {
        AbstractBuffer<byte>? buffer = null;
        Assert.Throws<ArgumentNullException>(() => buffer!.AvailableSlice());
    }

    private sealed class TestChunk : ReadOnlySequenceSegment<byte>
    {
        public TestChunk(ReadOnlyMemory<byte> memory) => Memory = memory;

        /// <summary>
        /// 将当前段与下一段链接并设置运行索引。
        /// </summary>
        public TestChunk LinkTo(TestChunk next)
        {
            Next = next;
            next.RunningIndex = RunningIndex + Memory.Length;
            return next;
        }
    }
}
