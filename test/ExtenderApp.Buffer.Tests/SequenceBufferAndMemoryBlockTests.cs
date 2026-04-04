using System;
using System.Buffers;
using ExtenderApp.Buffer;
using ExtenderApp.Buffer.MemoryBlocks;

namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 针对 <see cref="SequenceBuffer{T}"/> 与 <see cref="MemoryBlock{T}"/> 的基本写入、切片与比较行为测试。
/// </summary>
public class SequenceBufferAndMemoryBlockTests
{
    /// <summary>
    /// 验证 <see cref="SequenceBuffer{byte}"/> 在多次写入时可以创建多段并保持已提交长度与内容正确。
    /// </summary>
    [Fact]
    public void SequenceBuffer_WriteMultipleSpans_ShouldCreateSegmentsAndKeepCommitted()
    {
        var buffer = SequenceBuffer<byte>.GetBuffer();
        try
        {
            var part1 = new byte[] { 1, 2, 3, 4 };
            var part2 = new byte[] { 5, 6, 7, 8, 9 };

            buffer.Write(part1);
            buffer.Write(part2);

            Assert.True(buffer.Committed >= part1.Length + part2.Length);
            var allBytes = ((ReadOnlySequence<byte>)buffer).ToArray();

            var expected = new byte[part1.Length + part2.Length];
            part1.CopyTo(expected, 0);
            part2.CopyTo(expected, part1.Length);

            Assert.Equal(expected, allBytes);
        }
        finally
        {
            buffer.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="SequenceBuffer{T}.Slice(long,long)"/> 可以基于已提交数据创建新的缓冲并保留子范围内容。
    /// </summary>
    [Fact]
    public void SequenceBuffer_Slice_ShouldReturnExpectedRange()
    {
        var buffer = SequenceBuffer<byte>.GetBuffer();
        try
        {
            buffer.Write(new byte[] { 10, 20, 30, 40, 50, 60 });

            var slice = buffer.Slice(1, 3);
            try
            {
                var bytes = ((ReadOnlySequence<byte>)slice).ToArray();
                Assert.Equal(new byte[] { 20, 30, 40 }, bytes);
            }
            finally
            {
                slice.TryRelease();
            }
        }
        finally
        {
            buffer.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="MemoryBlock{byte}.TryCopyTo(MemoryBlock{byte})"/> 仅在目标有足够空间时返回 true 且内容一致。
    /// </summary>
    [Fact]
    public void MemoryBlock_TryCopyTo_ShouldRespectAvailableAndCopyBytes()
    {
        var source = MemoryBlock<byte>.GetBuffer(new byte[] { 1, 2, 3, 4 });
        var targetEnough = MemoryBlock<byte>.GetBuffer(8);
        var targetSmall = MemoryBlock<byte>.GetBuffer(2);

        try
        {
            Assert.True(source.TryCopyTo(targetEnough));
            Assert.False(source.TryCopyTo(targetSmall));

            var copied = targetEnough.CommittedSpan.Slice(0, (int)source.Committed).ToArray();
            Assert.Equal(source.CommittedSpan.ToArray(), copied);
        }
        finally
        {
            source.Dispose();
            targetEnough.Dispose();
            targetSmall.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="MemoryBlock{byte}.Reverse()"/> 与 <see cref="MemoryBlock{byte}.Reverse(int,int)"/> 能正确反转已提交区域。
    /// </summary>
    [Fact]
    public void MemoryBlock_ReverseAndReverseRange_ShouldReverseCommittedData()
    {
        var block = MemoryBlock<byte>.GetBuffer(new byte[] { 1, 2, 3, 4, 5 });
        try
        {
            block.Reverse(1, 3);
            Assert.Equal(new byte[] { 1, 4, 3, 2, 5 }, block.CommittedSpan.ToArray());

            block.Reverse();
            Assert.Equal(new byte[] { 5, 2, 3, 4, 1 }, block.CommittedSpan.ToArray());
        }
        finally
        {
            block.Dispose();
        }
    }
}

