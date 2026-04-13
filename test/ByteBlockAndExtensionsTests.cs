using System;
using System.Text;
using ExtenderApp.Buffer;
using ExtenderApp.Buffer.MemoryBlocks;

namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 针对 <see cref="ByteBlock" /> 及其扩展方法的功能与边界行为测试。
/// </summary>
public class ByteBlockAndExtensionsTests
{
    /// <summary>
    /// 验证默认构造的 <see cref="ByteBlock" /> 能够写入与扩容，并通过 <see cref="ByteBlock.ToArray" /> 正确还原数据。
    /// </summary>
    [Fact]
    public void ByteBlock_DefaultConstructor_WriteAndGrow_ToArrayMatches()
    {
        var data1 = new byte[] { 1, 2, 3, 4 };
        var data2 = new byte[] { 5, 6, 7, 8, 9 };

        var block = new ByteBlock();
        block.Write(data1);
        block.Write(data2);

        var expected = new byte[data1.Length + data2.Length];
        data1.CopyTo(expected, 0);
        data2.CopyTo(expected, data1.Length);

        var result = block.ToArray();

        Assert.Equal(expected, result);
        Assert.Equal(expected.Length, block.Committed);
        Assert.True(block.Capacity >= block.Committed);
    }

    /// <summary>
    /// 验证使用指定容量构造的 <see cref="ByteBlock" /> 能在容量边界附近写入并保持已提交长度正确。
    /// </summary>
    [Fact]
    public void ByteBlock_SpecificCapacity_WriteNearCapacity_CommittedCorrect()
    {
        using var block = new ByteBlock(8);
        block.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        Assert.Equal(8, block.Committed);
        Assert.True(block.Capacity >= 8);
    }

    /// <summary>
    /// 验证对 <see cref="ByteBlock" /> 写空数组不会改变已提交长度。
    /// </summary>
    [Fact]
    public void ByteBlock_WriteEmptyArray_ShouldNotChangeCommitted()
    {
        var block = new ByteBlock();
        var before = block.Committed;
        block.Write(Array.Empty<byte>());
        Assert.Equal(before, block.Committed);
    }

    /// <summary>
    /// 验证 <see cref="MemoryBlock{byte}.CommittedSpan" /> 能够直接反映已写入内容。
    /// </summary>
    [Fact]
    public void ByteBlock_CommittedSpan_ShouldExposeWrittenBytes()
    {
        var block = new ByteBlock();
        var payload = new byte[] { 10, 11, 12, 13 };
        block.Write(payload);

        var span = block.CommittedSpan;
        Assert.Equal(payload.Length, span.Length);
        Assert.True(span.SequenceEqual(payload));
    }

    /// <summary>
    /// 验证 <see cref="ByteBlockExtensions.WriteBoolean" /> 能写入布尔值并通过 <see cref="BinaryPrimitivesExtensions.ReadBoolean" /> 正确读取。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ByteBlockExtensions_WriteBoolean_CanBeReadBack(bool value)
    {
        var block = new ByteBlock();
        block.WriteBoolean(value);

        var span = block.CommittedSpan;
        Assert.Equal(1, span.Length);
        var read = ((ReadOnlySpan<byte>)span).ReadBoolean();
        Assert.Equal(value, read);
    }

    /// <summary>
    /// 验证 <see cref="ByteBlockExtensions.WriteGuid" /> 能写入 Guid 并通过 <see cref="BinaryPrimitivesExtensions.ReadGuid" /> 正确读取。
    /// </summary>
    [Fact]
    public void ByteBlockExtensions_WriteGuid_CanBeReadBack()
    {
        var guid = Guid.Parse("00112233-4455-6677-8899-AABBCCDDEEFF");

        var block = new ByteBlock();
        block.WriteGuid(guid);

        var span = block.CommittedSpan;
        Assert.Equal(16, span.Length);

        var read = ((ReadOnlySpan<byte>)span).ReadGuid();
        Assert.Equal(guid, read);
    }

    /// <summary>
    /// 验证 <see cref="ByteBlockExtensions.WriteDateTime" /> 能写入 ticks 并通过 <see cref="BinaryPrimitivesExtensions.ReadInt64" /> 正确还原。
    /// </summary>
    [Fact]
    public void ByteBlockExtensions_WriteDateTime_CanRestoreTicks()
    {
        var dt = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        var block = new ByteBlock();
        block.WriteDateTime(dt, isBigEndian: true);

        var span = block.CommittedSpan;
        Assert.Equal(sizeof(long), span.Length);

        var ticks = ((ReadOnlySpan<byte>)span).ReadInt64(isBigEndian: true);
        Assert.Equal(dt.Ticks, ticks);
    }

    /// <summary>
    /// 验证 <see cref="ByteBlockExtensions.WriteString" /> 能以指定编码写入字符串并保留字节内容。
    /// </summary>
    [Theory]
    [InlineData("Hello", "utf-8")]
    [InlineData("缓冲区测试", "utf-8")]
    public void ByteBlockExtensions_WriteString_CommittedBytesMatchEncoding(string text, string encodingName)
    {
        var encoding = Encoding.GetEncoding(encodingName);
        var block = new ByteBlock();

        block.WriteString(text, encoding);

        var expected = encoding.GetBytes(text);
        var span = block.CommittedSpan;

        Assert.Equal(expected.Length, span.Length);
        Assert.True(span.SequenceEqual(expected));
    }

    /// <summary>
    /// 验证 <see cref="ByteBlockExtensions.WriteString" /> 写入空或 null 字符串时不会改变已提交长度。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ByteBlockExtensions_WriteString_NullOrEmpty_NoEffect(string? text)
    {
        var block = new ByteBlock();
        var before = block.Committed;

        block.WriteString(text, Encoding.UTF8);

        Assert.Equal(before, block.Committed);
    }
}