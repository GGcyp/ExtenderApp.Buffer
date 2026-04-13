using System;
using ExtenderApp.Buffer;

namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 针对 <see cref="SpanReader{T}"/> 与 <see cref="SpanWriter{T}"/> 的顺序读写与位置管理测试。
/// </summary>
public class SpanReaderWriterTests
{
    /// <summary>
    /// 验证 <see cref="SpanWriter{T}"/> 可以顺序写入元素并通过 <see cref="SpanReader{T}"/> 正确读取。
    /// </summary>
    [Fact]
    public void SpanWriter_Then_SpanReader_ShouldRoundTrip()
    {
        Span<byte> buffer = stackalloc byte[10];
        var writer = new SpanWriter<byte>(buffer);

        writer.Write(new byte[] { 1, 2, 3, 4, 5 });

        Assert.Equal(5, writer.Consumed);
        Assert.False(writer.IsCompleted);

        var reader = new SpanReader<byte>(buffer.Slice(0, writer.Consumed));
        Assert.Equal(0, reader.Consumed);
        Assert.Equal(5, reader.Remaining);

        Assert.True(reader.TryRead(out var a));
        Assert.Equal(1, a);
        Assert.True(reader.TryRead(out var b));
        Assert.Equal(2, b);

        var remainingSpan = reader.UnreadSpan;
        Assert.Equal(new byte[] { 3, 4, 5 }, remainingSpan.ToArray());

        var readCount = reader.Read(stackalloc byte[3]);
        Assert.Equal(3, readCount);
        Assert.True(reader.IsCompleted);
    }

    /// <summary>
    /// 验证 <see cref="SpanWriter{T}.TryWrite"/> 与 <see cref="SpanReader{T}.TryRead"/> 在边界条件下的行为。
    /// </summary>
    [Fact]
    public void SpanWriterAndReader_TryMethods_ShouldRespectRemaining()
    {
        Span<int> buffer = stackalloc int[2];
        var writer = new SpanWriter<int>(buffer);

        Assert.True(writer.TryWrite(42));
        Assert.True(writer.TryWrite(43));
        Assert.False(writer.TryWrite(44));
        Assert.True(writer.IsCompleted);

        var reader = new SpanReader<int>(buffer);
        Assert.True(reader.TryRead(out var v1));
        Assert.True(reader.TryRead(out var v2));
        Assert.Equal(42, v1);
        Assert.Equal(43, v2);
        Assert.False(reader.TryRead(out _));
        Assert.True(reader.IsCompleted);
    }

    /// <summary>
    /// 验证 <see cref="SpanReader{T}.Advance"/> 与 <see cref="SpanReader{T}.Rewind"/> 可以正确推进与回退位置。
    /// </summary>
    [Fact]
    public void SpanReader_AdvanceAndRewind_ShouldUpdateConsumed()
    {
        var data = new byte[] { 10, 20, 30, 40 };
        var reader = new SpanReader<byte>(data);

        reader.Advance(2);
        Assert.Equal(2, reader.Consumed);
        Assert.Equal(2, reader.Remaining);

        reader.Rewind(1);
        Assert.Equal(1, reader.Consumed);

        Assert.True(reader.TryRead(out var value));
        Assert.Equal(20, value);
    }

    /// <summary>
    /// 验证 <see cref="SpanWriter{T}.Rewind"/> 与 <see cref="SpanWriter{T}.Reset"/> 能够回退或重置写入位置。
    /// </summary>
    [Fact]
    public void SpanWriter_RewindAndReset_ShouldUpdateConsumed()
    {
        Span<byte> buffer = stackalloc byte[4];
        var writer = new SpanWriter<byte>(buffer);

        writer.Write(new byte[] { 1, 2, 3 });
        Assert.Equal(3, writer.Consumed);

        writer.Rewind(1);
        Assert.Equal(2, writer.Consumed);

        writer.Reset();
        Assert.Equal(0, writer.Consumed);
        Assert.Equal(4, writer.Remaining);
    }
}

