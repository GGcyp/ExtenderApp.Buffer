using ExtenderApp.Buffer.Primitives;

namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 覆盖 <see cref="AbstractBuffer"/> 静态工厂与 <see cref="FreezeObject"/> 冻结语义。
/// </summary>
public class AbstractBufferFactoryAndFreezeObjectTests
{
    /// <summary>
    /// 验证 <see cref="AbstractBuffer.GetBlock{T}(ReadOnlySpan{T})"/> 返回可写块且内容与输入一致。
    /// </summary>
    [Fact]
    public void AbstractBuffer_GetBlock_FromReadOnlySpan_WritableCopy()
    {
        ReadOnlySpan<byte> src = stackalloc byte[] { 5, 6, 7 };
        var block = AbstractBuffer.GetBlock(src);
        try
        {
            Assert.True(block.Committed >= src.Length);
            var reader = block.GetReader();
            try
            {
                var dest = new byte[src.Length];
                Assert.True(reader.TryRead(dest));
                Assert.True(src.SequenceEqual(dest));
            }
            finally
            {
                reader.Release();
            }
        }
        finally
        {
            block.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="AbstractBuffer.GetSequence{T}()"/> 得到空序列缓冲且已提交为 0。
    /// </summary>
    [Fact]
    public void AbstractBuffer_GetSequence_Empty_HasZeroCommitted()
    {
        var seq = AbstractBuffer.GetSequence<byte>();
        try
        {
            Assert.Equal(0, seq.Committed);
        }
        finally
        {
            seq.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="FreezeObject.Freeze"/> / <see cref="FreezeObject.Unfreeze"/> 嵌套与 <see cref="FreezeObject.CheckFrozen"/>。
    /// </summary>
    [Fact]
    public void FreezeObject_NestedFreeze_CheckFrozenThrowsUntilBalanced()
    {
        using var fo = new FreezeObject();
        Assert.False(fo.IsFrozen);
        fo.Freeze();
        Assert.True(fo.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => fo.CheckFrozen());

        fo.Unfreeze();
        Assert.False(fo.IsFrozen);
        fo.CheckFrozen();
    }
}
