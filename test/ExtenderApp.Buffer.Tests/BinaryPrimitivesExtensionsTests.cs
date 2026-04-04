using System;
using System.Linq;
using ExtenderApp.Buffer;

namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 针对 <see cref="BinaryPrimitivesExtensions"/> 的读写往返测试，验证多种基础类型在大小端模式下的正确性。
/// </summary>
public class BinaryPrimitivesExtensionsTests
{
    /// <summary>
    /// 验证整型读写在大小端模式下能够精确往返。
    /// </summary>
    [Theory]
    [InlineData(short.MinValue, true)]
    [InlineData(short.MinValue, false)]
    [InlineData((short)0, true)]
    [InlineData((short)0, false)]
    [InlineData(short.MaxValue, true)]
    [InlineData(short.MaxValue, false)]
    public void Int16_RoundTrip(short value, bool isBigEndian)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        buffer.WriteInt16(value, isBigEndian);

        var read = ((ReadOnlySpan<byte>)buffer).ReadInt16(isBigEndian);
        Assert.Equal(value, read);
    }

    /// <summary>
    /// 验证 32 位整型在大小端模式下读写往返正确。
    /// </summary>
    [Theory]
    [InlineData(int.MinValue, true)]
    [InlineData(int.MinValue, false)]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(int.MaxValue, true)]
    [InlineData(int.MaxValue, false)]
    [InlineData(123456789, true)]
    [InlineData(123456789, false)]
    public void Int32_RoundTrip(int value, bool isBigEndian)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        buffer.WriteInt32(value, isBigEndian);

        var read = ((ReadOnlySpan<byte>)buffer).ReadInt32(isBigEndian);
        Assert.Equal(value, read);
    }

    /// <summary>
    /// 验证 64 位整型在大小端模式下读写往返正确。
    /// </summary>
    [Theory]
    [InlineData(long.MinValue, true)]
    [InlineData(long.MinValue, false)]
    [InlineData(0L, true)]
    [InlineData(0L, false)]
    [InlineData(long.MaxValue, true)]
    [InlineData(long.MaxValue, false)]
    [InlineData(987654321012345678L, true)]
    [InlineData(987654321012345678L, false)]
    public void Int64_RoundTrip(long value, bool isBigEndian)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        buffer.WriteInt64(value, isBigEndian);

        var read = ((ReadOnlySpan<byte>)buffer).ReadInt64(isBigEndian);
        Assert.Equal(value, read);
    }

    /// <summary>
    /// 验证无符号整型在大小端模式下读写往返正确。
    /// </summary>
    [Theory]
    [InlineData((ushort)0, true)]
    [InlineData((ushort)0, false)]
    [InlineData(ushort.MaxValue, true)]
    [InlineData(ushort.MaxValue, false)]
    public void UInt16_RoundTrip(ushort value, bool isBigEndian)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        buffer.WriteUInt16(value, isBigEndian);

        var read = ((ReadOnlySpan<byte>)buffer).ReadUInt16(isBigEndian);
        Assert.Equal(value, read);
    }

    /// <summary>
    /// 验证 32 位与 64 位无符号整型在大小端模式下读写往返正确。
    /// </summary>
    [Theory]
    [InlineData(0u, true)]
    [InlineData(0u, false)]
    [InlineData(uint.MaxValue, true)]
    [InlineData(uint.MaxValue, false)]
    [InlineData(1234567890u, true)]
    [InlineData(1234567890u, false)]
    public void UInt32_RoundTrip(uint value, bool isBigEndian)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        buffer.WriteUInt32(value, isBigEndian);

        var read = ((ReadOnlySpan<byte>)buffer).ReadUInt32(isBigEndian);
        Assert.Equal(value, read);
    }

    /// <summary>
    /// 验证 64 位无符号整型在大小端模式下读写往返正确。
    /// </summary>
    [Theory]
    [InlineData(0ul, true)]
    [InlineData(0ul, false)]
    [InlineData(ulong.MaxValue, true)]
    [InlineData(ulong.MaxValue, false)]
    public void UInt64_RoundTrip(ulong value, bool isBigEndian)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        buffer.WriteUInt64(value, isBigEndian);

        var read = ((ReadOnlySpan<byte>)buffer).ReadUInt64(isBigEndian);
        Assert.Equal(value, read);
    }

    /// <summary>
    /// 验证单精度浮点数在大小端模式下读写往返正确。
    /// </summary>
    [Theory]
    [InlineData(0f, true)]
    [InlineData(0f, false)]
    [InlineData(1.5f, true)]
    [InlineData(1.5f, false)]
    [InlineData(float.MinValue, true)]
    [InlineData(float.MaxValue, false)]
    public void Single_RoundTrip(float value, bool isBigEndian)
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        buffer.WriteSingle(value, isBigEndian);

        var read = ((ReadOnlySpan<byte>)buffer).ReadSingle(isBigEndian);
        Assert.Equal(value, read);
    }

    /// <summary>
    /// 验证双精度浮点数在大小端模式下读写往返正确。
    /// </summary>
    [Theory]
    [InlineData(0d, true)]
    [InlineData(0d, false)]
    [InlineData(1.5d, true)]
    [InlineData(1.5d, false)]
    [InlineData(double.MinValue, true)]
    [InlineData(double.MaxValue, false)]
    public void Double_RoundTrip(double value, bool isBigEndian)
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        buffer.WriteDouble(value, isBigEndian);

        var read = ((ReadOnlySpan<byte>)buffer).ReadDouble(isBigEndian);
        Assert.Equal(value, read);
    }

    /// <summary>
    /// 验证 decimal 在字节数组中的写入和读取能够精确还原。
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("123456789.987654321")]
    [InlineData("-123456789.987654321")]
    public void Decimal_RoundTrip(string text)
    {
        var value = decimal.Parse(text);
        Span<byte> buffer = stackalloc byte[16];
        buffer.WriteDecimal(value);

        var read = ((ReadOnlySpan<byte>)buffer).ReadDecimal();
        Assert.Equal(value, read);
    }

    /// <summary>
    /// 验证 Guid 写入与读取的往返行为。
    /// </summary>
    [Fact]
    public void Guid_RoundTrip()
    {
        var guid = Guid.Parse("00112233-4455-6677-8899-AABBCCDDEEFF");
        Span<byte> buffer = stackalloc byte[16];
        buffer.WriteGuid(guid);

        var read = ((ReadOnlySpan<byte>)buffer).ReadGuid();
        Assert.Equal(guid, read);
    }

    /// <summary>
    /// 验证布尔值写入与读取的往返行为。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Boolean_RoundTrip(bool value)
    {
        Span<byte> buffer = stackalloc byte[1];
        buffer.WriteBoolean(value);

        var read = ((ReadOnlySpan<byte>)buffer).ReadBoolean();
        Assert.Equal(value, read);
    }
}

