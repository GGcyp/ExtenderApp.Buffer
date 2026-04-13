using System.Buffers;
using ExtenderApp.Buffer.Streams;

namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 覆盖 <see cref="AbstractBufferStream"/>、<see cref="AbstractBufferStreamDecorator"/>、
/// <see cref="StreamDecorator"/>、<see cref="BinaryReaderAdapter"/>、<see cref="BinaryWriterAdapter"/> 与 <see cref="NativeByteMemory"/>。
/// </summary>
public class StreamDecoratorBinaryAdapterAndNativeTests
{
    /// <summary>
    /// 用于构造多段 <see cref="ReadOnlySequence{T}"/> 的段节点。
    /// </summary>
    private sealed class BufferChunk : ReadOnlySequenceSegment<byte>
    {
        public BufferChunk(ReadOnlyMemory<byte> memory) => Memory = memory;

        /// <summary>
        /// 将当前段与下一段链接并设置运行索引。
        /// </summary>
        public BufferChunk LinkTo(BufferChunk next)
        {
            Next = next;
            next.RunningIndex = RunningIndex + Memory.Length;
            return next;
        }
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferStream"/> 在附加 <see cref="MemoryBlock{byte}"/> 后可顺序读出已提交字节。
    /// </summary>
    [Fact]
    public void AbstractBufferStream_Read_WalksCommittedBytes()
    {
        var block = MemoryBlock<byte>.GetBuffer(new byte[] { 1, 2, 3, 4 });
        using var stream = new AbstractBufferStream(block);
        stream.SetLength(block.Committed);

        var buf = new byte[4];
        Assert.Equal(4, stream.Read(buf));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, buf);
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferStreamDecorator.SetReadBuffer"/> 后 <see cref="Stream.Read(Span{byte})"/> 能读出数据。
    /// </summary>
    [Fact]
    public void AbstractBufferStreamDecorator_SetReadBuffer_Read_ReturnsPayload()
    {
        var block = MemoryBlock<byte>.GetBuffer(new byte[] { 9, 8, 7 });
        using var deco = new AbstractBufferStreamDecorator();
        deco.SetReadBuffer(block);

        var buf = new byte[3];
        Assert.Equal(3, deco.Read(buf));
        Assert.Equal(new byte[] { 9, 8, 7 }, buf);
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferStreamDecorator.Write(ReadOnlySpan{byte})"/> 写入后可通过 <see cref="AbstractBufferStreamDecorator.GetWriteBuffer"/> 取回块。
    /// </summary>
    [Fact]
    public void AbstractBufferStreamDecorator_Write_GetWriteBuffer_HasCommittedBytes()
    {
        using var deco = new AbstractBufferStreamDecorator();
        deco.Write(new byte[] { 1, 2, 3 });

        var wb = deco.GetWriteBuffer();
        Assert.NotNull(wb);
        try
        {
            Assert.Equal(3, wb!.Committed);
            Assert.Equal(new byte[] { 1, 2, 3 }, wb.CommittedSpan.ToArray());
        }
        finally
        {
            wb!.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="StreamDecorator"/> 在设置内部流后可转发读写。
    /// </summary>
    [Fact]
    public void StreamDecorator_SetInnerStream_ReadWrite_RoundTrip()
    {
        using var inner = new MemoryStream();
        using var deco = new StreamDecorator(leaveInnerStreamOpen: false);
        deco.SetInnerStream(inner);

        deco.WriteByte(0xFE);
        Assert.Equal(1, deco.Length);
        deco.Position = 0;
        Assert.Equal(0xFE, deco.ReadByte());
    }

    /// <summary>
    /// 验证 <see cref="BinaryReaderAdapter"/> 在单段序列上可预览与读取字节。
    /// </summary>
    [Fact]
    public void BinaryReaderAdapter_TryPeekTryRead_OnSingleSegment()
    {
        var seq = new ReadOnlySequence<byte>(new byte[] { 11, 22, 33 });
        var adapter = new BinaryReaderAdapter(seq);

        Assert.True(adapter.TryPeek(out var p));
        Assert.Equal(11, p);

        Assert.True(adapter.TryRead(out var a));
        Assert.Equal(11, a);

        Span<byte> two = stackalloc byte[2];
        Assert.True(adapter.TryRead(two));
        Assert.Equal(new byte[] { 22, 33 }, two.ToArray());
        Assert.True(adapter.End);
    }

    /// <summary>
    /// 验证 <see cref="BinaryReaderAdapter.TryPeek(System.Span{byte})"/> 在多段序列上拷贝预览且不推进消费位置。
    /// </summary>
    [Fact]
    public void BinaryReaderAdapter_MultiSegment_TryPeekSpan_DoesNotAdvance()
    {
        var c0 = new BufferChunk(new byte[] { 1, 2 });
        var c1 = c0.LinkTo(new BufferChunk(new byte[] { 3 }));
        var seq = new ReadOnlySequence<byte>(c0, 0, c1, c1.Memory.Length);

        var adapter = new BinaryReaderAdapter(seq);
        Span<byte> peek = stackalloc byte[3];
        Assert.True(adapter.TryPeek(peek));
        Assert.Equal(new byte[] { 1, 2, 3 }, peek.ToArray());
        Assert.Equal(0, adapter.Consumed);

        Assert.True(adapter.TryRead(peek));
        Assert.Equal(new byte[] { 1, 2, 3 }, peek.ToArray());
        Assert.True(adapter.End);
    }

    /// <summary>
    /// 验证 <see cref="BinaryReaderAdapter.TryPeek(System.Span{byte})"/> 在剩余字节不足时返回 <c>false</c>。
    /// </summary>
    [Fact]
    public void BinaryReaderAdapter_MultiSegment_TryPeekSpan_Insufficient_ReturnsFalse()
    {
        var c0 = new BufferChunk(new byte[] { 1 });
        var c1 = c0.LinkTo(new BufferChunk(new byte[] { 2 }));
        var seq = new ReadOnlySequence<byte>(c0, 0, c1, c1.Memory.Length);

        var adapter = new BinaryReaderAdapter(seq);
        Span<byte> peek = stackalloc byte[3];
        Assert.False(adapter.TryPeek(peek));
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferStream.Flush"/> 在释放后抛出 <see cref="ObjectDisposedException"/>。
    /// </summary>
    [Fact]
    public void AbstractBufferStream_Flush_AfterDispose_Throws()
    {
        var stream = new AbstractBufferStream();
        stream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => stream.Flush());
    }

    /// <summary>
    /// 验证未附加缓冲区时 <see cref="AbstractBufferStream.Read(Span{byte})"/> 抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    [Fact]
    public void AbstractBufferStream_Read_WithoutBuffer_Throws()
    {
        using var stream = new AbstractBufferStream();
        stream.SetLength(0);
        Assert.Throws<InvalidOperationException>(() => stream.Read(stackalloc byte[1]));
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferStream.Seek(long, SeekOrigin)"/> 对非法 <see cref="SeekOrigin"/> 抛出 <see cref="ArgumentOutOfRangeException"/>。
    /// </summary>
    [Fact]
    public void AbstractBufferStream_Seek_InvalidOrigin_Throws()
    {
        using var stream = new AbstractBufferStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Seek(0, (SeekOrigin)99));
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferStream.Seek(long, SeekOrigin)"/> 在结果为负位置时抛出 <see cref="IndexOutOfRangeException"/>。
    /// </summary>
    [Fact]
    public void AbstractBufferStream_Seek_NegativeResult_Throws()
    {
        using var stream = new AbstractBufferStream();
        Assert.Throws<IndexOutOfRangeException>(() => stream.Seek(-1, SeekOrigin.Begin));
    }

    /// <summary>
    /// 验证未设置读取缓冲区时 <see cref="AbstractBufferStreamDecorator.Length"/> 为 0。
    /// </summary>
    [Fact]
    public void AbstractBufferStreamDecorator_Length_WithoutReadBuffer_IsZero()
    {
        using var deco = new AbstractBufferStreamDecorator();
        Assert.Equal(0, deco.Length);
    }

    /// <summary>
    /// 验证未设置读取缓冲区时 <see cref="AbstractBufferStreamDecorator.Read(Span{byte})"/> 抛出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void AbstractBufferStreamDecorator_Read_WithoutSetReadBuffer_Throws()
    {
        using var deco = new AbstractBufferStreamDecorator();
        Assert.Throws<ArgumentNullException>(() => deco.Read(stackalloc byte[1]));
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferStreamDecorator.Flush"/> 为空操作且不抛异常。
    /// </summary>
    [Fact]
    public void AbstractBufferStreamDecorator_Flush_NoOp()
    {
        using var deco = new AbstractBufferStreamDecorator();
        deco.Flush();
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferStreamDecorator.Seek(long, SeekOrigin)"/> 抛出 <see cref="NotImplementedException"/>。
    /// </summary>
    [Fact]
    public void AbstractBufferStreamDecorator_Seek_NotImplemented()
    {
        using var deco = new AbstractBufferStreamDecorator();
        Assert.Throws<NotImplementedException>(() => deco.Seek(0, SeekOrigin.Begin));
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferStreamDecorator.Position"/> 读写均抛出 <see cref="NotImplementedException"/>。
    /// </summary>
    [Fact]
    public void AbstractBufferStreamDecorator_Position_NotImplemented()
    {
        using var deco = new AbstractBufferStreamDecorator();
        Assert.Throws<NotImplementedException>(() => _ = deco.Position);
        Assert.Throws<NotImplementedException>(() => deco.Position = 0);
    }

    /// <summary>
    /// 验证 <see cref="BinaryWriterAdapter"/> 向 <see cref="ArrayBufferWriter{T}"/> 写入后已提交长度与内容正确。
    /// </summary>
    [Fact]
    public void BinaryWriterAdapter_WriteByteAndSpan_CommittedMatches()
    {
        var writer = new ArrayBufferWriter<byte>();
        var adapter = new BinaryWriterAdapter(writer);
        adapter.Write((byte)0xCD);
        adapter.Write(new byte[] { 0xEF, 0x10 });

        Assert.Equal(3, writer.WrittenCount);
        Assert.Equal(new byte[] { 0xCD, 0xEF, 0x10 }, writer.WrittenSpan.ToArray());
    }

    /// <summary>
    /// 验证 <see cref="NativeByteMemory"/> 从托管数据构造、<see cref="NativeByteMemory.CopyTo(Span{byte})"/> 与释放。
    /// </summary>
    [Fact]
    public void NativeByteMemory_FromSpan_CopyTo_Dispose()
    {
        var src = new byte[] { 1, 2, 3, 4 };
        using var native = new NativeByteMemory(src);
        Assert.Equal(src.Length, native.Length);
        Assert.False(native.IsEmpty);

        Span<byte> dest = stackalloc byte[4];
        native.CopyTo(dest);
        Assert.True(src.AsSpan().SequenceEqual(dest));

        var other = new NativeByteMemory(4);
        try
        {
            native.CopyTo(other);
            Assert.True(src.AsSpan().SequenceEqual(other.Span));
        }
        finally
        {
            other.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="NativeIntPtr{int}"/> 空实例与相等比较语义。
    /// </summary>
    [Fact]
    public void NativeIntPtr_Int_IsEmpty_AndEquals()
    {
        NativeIntPtr<int> a = NativeIntPtr<int>.Empty;
        Assert.True(a.IsEmpty);

        NativeIntPtr<int> b = default;
        Assert.True(b.IsEmpty);
        Assert.Equal(a, b);
    }
}
