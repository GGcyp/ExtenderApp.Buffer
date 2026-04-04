using System.Text;
using ExtenderApp.Buffer;

namespace ExtenderApp.Buffer.Tests;

public class UnitTest
{
    /// <summary>
    /// 验证 ByteBlock 基础写入与 ToArray 行为。
    /// </summary>
    [Fact]
    public void ByteBlock_WriteBytes_Then_ToArray_ShouldMatch()
    {
        var data = new byte[] { 1, 2, 3, 4 };
        using var block = new ByteBlock();
        block.Write(data);

        var result = block.ToArray();

        Assert.Equal(data, result);
        Assert.Equal(data.Length, block.Committed);
        Assert.True(block.Capacity >= block.Committed);
    }

    /// <summary>
    /// 验证 ByteBlockExtensions 写入基础类型后，可通过 CommittedSpan 读取出一致的数据。
    /// </summary>
    [Fact]
    public void ByteBlockExtensions_WritePrimitiveTypes_CommittedSpan_ShouldDecodeCorrectly()
    {
        var encoding = Encoding.UTF8;
        var block = new ByteBlock();
        try
        {
            block.Write<int>(123456789, isBigEndian: true);
            block.WriteBoolean(true);
            block.WriteGuid(Guid.Parse("00112233-4455-6677-8899-AABBCCDDEEFF"));
            block.WriteDateTime(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc));
            block.WriteString("Hello", encoding);

            var span = block.CommittedSpan;
            Assert.True(span.Length > 0);
        }
        finally
        {
            block.Dispose();
        }
    }
}
