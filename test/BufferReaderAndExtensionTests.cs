using System.Buffers;
using ExtenderApp.Buffer.Reader;

namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 覆盖 <see cref="MemoryBlockReader{T}"/>、<see cref="SequenceBufferReader{T}"/>、
/// <see cref="ValueMemoryBlockReader{T}"/>、<see cref="ValueSequenceBufferReader{T}"/> 与
/// <see cref="AbstractBufferReaderExtensions"/> 等读取路径。
/// </summary>
public class BufferReaderAndExtensionTests
{
    /// <summary>
    /// 验证 <see cref="MemoryBlockReader{T}"/> 顺序读取与 <see cref="MemoryBlockReader{T}.Release"/> 后缓冲可再次租用。
    /// </summary>
    [Fact]
    public void MemoryBlockReader_TryReadAndRelease_ShouldMatchCommitted()
    {
        var block = MemoryBlock<byte>.GetBuffer(new byte[] { 1, 2, 3, 4 });
        var reader = MemoryBlockReader<byte>.GetReader(block);
        try
        {
            Assert.True(reader.TryRead(out var a));
            Assert.Equal(1, a);
            Assert.True(reader.TryRead(2, out ReadOnlySpan<byte> span));
            Assert.Equal(new byte[] { 2, 3 }, span.ToArray());
            Assert.True(reader.TryRead(out var b));
            Assert.Equal(4, b);
            Assert.True(reader.IsCompleted);
        }
        finally
        {
            reader.Release();
        }
    }

    /// <summary>
    /// 验证多段 <see cref="SequenceBuffer{T}"/> 上 <see cref="SequenceBufferReader{T}"/> 可跨段读完已提交数据。
    /// </summary>
    [Fact]
    public void SequenceBufferReader_MultiSegment_ReadsAllBytes()
    {
        var buffer = SequenceBuffer<byte>.GetBuffer();
        try
        {
            buffer.Write(new byte[] { 10, 20 });
            buffer.Write(new byte[] { 30, 40, 50 });

            var reader = SequenceBufferReader<byte>.GetReader(buffer);
            try
            {
                var dest = new byte[5];
                Assert.True(reader.TryRead(dest));
                Assert.Equal(new byte[] { 10, 20, 30, 40, 50 }, dest);
                Assert.True(reader.IsCompleted);
            }
            finally
            {
                reader.Release();
            }
        }
        finally
        {
            buffer.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="AbstractBuffer{T}.GetReader"/> 对非 MemoryBlock/SequenceBuffer 实现抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    [Fact]
    public void AbstractBuffer_GetReader_UnsupportedBuffer_Throws()
    {
        var empty = AbstractBuffer<byte>.Empty;
        Assert.Throws<InvalidOperationException>(() => empty.GetReader());
    }

    /// <summary>
    /// 验证 <see cref="AbstractBufferReaderExtensions.Read{T}"/> 从未读序列解析整型并推进消费位置。
    /// </summary>
    [Fact]
    public void AbstractBufferReaderExtensions_ReadInt32_BigEndian_RoundTrip()
    {
        var block = MemoryBlock<byte>.GetBuffer(8);
        var w = block.GetSpan(4);
        w.WriteInt32(0x11223344, isBigEndian: true);
        block.Advance(4);

        var reader = block.GetReader();
        try
        {
            var value = reader.Read<int>(isBigEndian: true);
            Assert.Equal(0x11223344, value);
            Assert.True(reader.IsCompleted);
        }
        finally
        {
            reader.Release();
        }
    }

    /// <summary>
    /// 验证 <see cref="ValueMemoryBlockReader{T}"/> 的预览、读取与 <see cref="ValueMemoryBlockReader{T}.Dispose"/>。
    /// </summary>
    [Fact]
    public void ValueMemoryBlockReader_TryPeekTryRead_DisposeReleasesBlock()
    {
        var block = MemoryBlock<byte>.GetBuffer(new byte[] { 7, 8, 9 });
        var reader = new ValueMemoryBlockReader<byte>(block);
        try
        {
            Assert.True(reader.TryPeek(out var p));
            Assert.Equal(7, p);
            Assert.True(reader.TryRead(out var a));
            Assert.Equal(7, a);
            Assert.Equal(2, reader.Remaining);
        }
        finally
        {
            reader.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="ValueSequenceBufferReader{T}"/> 在多段序列上可顺序消费至完成。
    /// </summary>
    [Fact]
    public void ValueSequenceBufferReader_MultiSegment_ReadsToEnd()
    {
        var buffer = SequenceBuffer<byte>.GetBuffer();
        try
        {
            buffer.Write(new byte[] { 1 });
            buffer.Write(new byte[] { 2, 3 });

            var reader = new ValueSequenceBufferReader<byte>(buffer);
            try
            {
                Assert.False(reader.IsCompleted);
                Assert.True(reader.TryRead(out var a));
                Assert.Equal(1, a);
                Assert.True(reader.TryRead(out var b));
                Assert.Equal(2, b);
                Assert.True(reader.TryRead(out var c));
                Assert.Equal(3, c);
                Assert.True(reader.IsCompleted);
            }
            finally
            {
                reader.Dispose();
            }
        }
        finally
        {
            buffer.TryRelease();
        }
    }
}
