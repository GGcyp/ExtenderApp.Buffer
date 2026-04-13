using System.Buffers;
using ExtenderApp.Buffer.SequenceBuffers;
using ExtenderApp.Buffer.ValueBuffers;

namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 覆盖 <see cref="FastSequence{T}"/>、<see cref="ValueSequenceBuffer{T}"/> 与 <see cref="FastSequenceExtensions"/>。
/// </summary>
public class FastSequenceAndValueSequenceBufferTests
{
    /// <summary>
    /// 验证 <see cref="FastSequence{T}.GetBuffer"/> 写入、已提交长度与 <see cref="FastSequence{T}.TryRelease"/> 回收。
    /// </summary>
    [Fact]
    public void FastSequence_GetSpanAdvance_TryRelease_CommittedMatches()
    {
        var seq = FastSequence<byte>.GetBuffer();
        try
        {
            var payload = new byte[] { 1, 2, 3, 4, 5 };
            var span = seq.GetSpan(payload.Length);
            payload.CopyTo(span);
            seq.Advance(payload.Length);

            Assert.Equal(payload.Length, seq.Committed);
            Assert.False(seq.IsEmpty);
            var all = ((ReadOnlySequence<byte>)seq).ToArray();
            Assert.Equal(payload, all);
        }
        finally
        {
            seq.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="FastSequenceExtensions.ToBuffer"/> 将单段序列转为 <see cref="AbstractBuffer{T}"/> 且内容一致。
    /// </summary>
    [Fact]
    public void FastSequence_ToBuffer_SingleSegment_CopiesBytes()
    {
        var seq = FastSequence<byte>.GetBuffer();
        AbstractBuffer<byte>? buffer = null;
        try
        {
            var data = new byte[] { 10, 20, 30 };
            var span = seq.GetSpan(data.Length);
            data.CopyTo(span);
            seq.Advance(data.Length);

            buffer = seq.ToBuffer();
            Assert.Equal(data.Length, buffer.Committed);
            var reader = buffer.GetReader();
            try
            {
                var dest = new byte[data.Length];
                Assert.True(reader.TryRead(dest));
                Assert.Equal(data, dest);
            }
            finally
            {
                reader.Release();
            }
        }
        finally
        {
            buffer?.TryRelease();
            seq.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="ValueSequenceBuffer{T}"/> 包装 <see cref="FastSequence{T}"/> 后可写入并读取已提交序列。
    /// </summary>
    [Fact]
    public void ValueSequenceBuffer_Write_Dispose_ReleasesUnderlying()
    {
        var fast = FastSequence<byte>.GetBuffer();
        var span = fast.GetSpan(2);
        span[0] = 0xAA;
        span[1] = 0xBB;
        fast.Advance(2);

        var vs = new ValueSequenceBuffer<byte>(fast);
        Assert.Equal(2, vs.Committed);
        ReadOnlySequence<byte> ro = vs;
        Assert.Equal(2, ro.Length);

        vs.Dispose();
    }
}
