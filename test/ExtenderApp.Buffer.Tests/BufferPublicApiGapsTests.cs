using System.Buffers;
using ExtenderApp.Buffer.MemoryBlocks;
using ExtenderApp.Buffer.SequenceBuffers;
using ExtenderApp.Buffer.ValueBuffers;

namespace ExtenderApp.Buffer.Tests;

/// <summary>
/// 针对计划中列出的公开 API 缺口：序列提供者、MemoryBlock 切片与克隆、段级操作、ByteBlock 与 ObjectPool 策略工厂等。
/// </summary>
public class BufferPublicApiGapsTests
{
    /// <summary>
    /// 用于构造多段 <see cref="ReadOnlySequence{T}"/> 的段节点。
    /// </summary>
    private sealed class BufferChunk : ReadOnlySequenceSegment<byte>
    {
        public BufferChunk(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

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
    /// 验证 <see cref="SequenceBufferProvider{T}.GetBuffer(ReadOnlySequence{T})"/> 在多段输入下内容一致且可 <see cref="AbstractBuffer{T}.TryRelease"/>。
    /// </summary>
    [Fact]
    public void SequenceBufferProvider_GetBuffer_FromMultiSegmentReadOnlySequence_RoundTripAndRelease()
    {
        var first = new BufferChunk(new byte[] { 1, 2 });
        var last = first.LinkTo(new BufferChunk(new byte[] { 3, 4, 5 }));
        var ros = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

        var buf = SequenceBufferProvider<byte>.Shared.GetBuffer(ros);
        try
        {
            Assert.Equal(5, buf.Committed);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, ((ReadOnlySequence<byte>)buf).ToArray());
        }
        finally
        {
            Assert.True(buf.TryRelease());
        }
    }

    /// <summary>
    /// 验证空 <see cref="ReadOnlySequence{T}"/> 经提供者得到零长度序列缓冲并可释放。
    /// </summary>
    [Fact]
    public void SequenceBufferProvider_GetBuffer_EmptySequence_ZeroCommitted()
    {
        var buf = SequenceBufferProvider<byte>.Shared.GetBuffer(ReadOnlySequence<byte>.Empty);
        try
        {
            Assert.Equal(0, buf.Committed);
        }
        finally
        {
            Assert.True(buf.TryRelease());
        }
    }

    /// <summary>
    /// 验证 <see cref="SequenceBufferProvider{T}.GetBuffer(ValueSequenceBuffer{T})"/> 与隐式转换得到等价的 <see cref="SequenceBuffer{T}"/>。
    /// </summary>
    [Fact]
    public void SequenceBufferProvider_GetBuffer_FromValueSequenceBuffer_MatchesFastSequenceContent()
    {
        var fast = FastSequence<byte>.GetBuffer();
        try
        {
            var span = fast.GetSpan(4);
            span[0] = 0xA1;
            span[1] = 0xA2;
            span[2] = 0xA3;
            span[3] = 0xA4;
            fast.Advance(4);

            var vs = new ValueSequenceBuffer<byte>(fast);
            var buf = SequenceBufferProvider<byte>.Shared.GetBuffer(vs);
            try
            {
                Assert.Equal(4, buf.Committed);
                Assert.Equal(new byte[] { 0xA1, 0xA2, 0xA3, 0xA4 }, ((ReadOnlySequence<byte>)buf).ToArray());
            }
            finally
            {
                Assert.True(buf.TryRelease());
            }
        }
        finally
        {
            fast.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="ValueSequenceBuffer{T}"/> 到 <see cref="SequenceBuffer{T}"/> 的隐式转换调用提供者并正确复制数据。
    /// </summary>
    [Fact]
    public void ValueSequenceBuffer_ImplicitToSequenceBuffer_UsesProvider()
    {
        var fast = FastSequence<byte>.GetBuffer();
        try
        {
            fast.GetSpan(1)[0] = 0xEE;
            fast.Advance(1);

            var vs = new ValueSequenceBuffer<byte>(fast);
            SequenceBuffer<byte> buf = vs;
            try
            {
                Assert.Equal(1, buf.Committed);
                Assert.Equal(0xEE, ((ReadOnlySequence<byte>)buf).FirstSpan[0]);
            }
            finally
            {
                Assert.True(buf.TryRelease());
            }
        }
        finally
        {
            fast.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="MemoryBlock{T}.Slice(long,long)"/> 得到已提交子范围副本且可独立释放。
    /// </summary>
    [Fact]
    public void MemoryBlock_Slice_SubRange_ContentAndRelease()
    {
        var block = MemoryBlock<byte>.GetBuffer(new byte[] { 10, 20, 30, 40 });
        try
        {
            var sub = (MemoryBlock<byte>)block.Slice(1, 2);
            try
            {
                Assert.Equal(new byte[] { 20, 30 }, sub.CommittedSpan.ToArray());
            }
            finally
            {
                Assert.True(sub.TryRelease());
            }
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="MemoryBlock{T}.Clone"/> 得到相同已提交数据的新块且与原块独立释放。
    /// </summary>
    [Fact]
    public void MemoryBlock_Clone_CopiesCommittedAndReleases()
    {
        var block = MemoryBlock<byte>.GetBuffer(new byte[] { 7, 8, 9 });
        try
        {
            var clone = (MemoryBlock<byte>)block.Clone();
            try
            {
                Assert.Equal(block.CommittedSpan.ToArray(), clone.CommittedSpan.ToArray());
            }
            finally
            {
                Assert.True(clone.TryRelease());
            }
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="MemoryBlock{T}"/> 相等性与运算符在相同已提交字节时一致。
    /// </summary>
    [Fact]
    public void MemoryBlock_Equals_AndOperators_WhenCommittedMatch()
    {
        var a = MemoryBlock<byte>.GetBuffer(new byte[] { 1, 2, 3 });
        var b = MemoryBlock<byte>.GetBuffer(new byte[] { 1, 2, 3 });
        try
        {
            Assert.True(a.Equals(b));
            Assert.True(a == b);
            Assert.False(a != b);
        }
        finally
        {
            a.Dispose();
            b.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="SequenceBuffer{T}.Append(SequenceBufferSegment{T})"/> 链接两段且序列内容正确。
    /// </summary>
    [Fact]
    public void SequenceBuffer_Append_TwoUnlinkedSegments_SequenceMatches()
    {
        var segProv = SequenceBufferSegmentProvider<byte>.Shared;
        var block1 = MemoryBlock<byte>.GetBuffer(new byte[] { 1, 2 });
        var block2 = MemoryBlock<byte>.GetBuffer(new byte[] { 3 });
        var s1 = segProv.GetSegment(block1);
        var s2 = segProv.GetSegment(block2);

        var buf = SequenceBuffer<byte>.GetBuffer();
        try
        {
            buf.Append(s1);
            buf.Append(s2);
            Assert.Equal(3, buf.Committed);
            Assert.Equal(new byte[] { 1, 2, 3 }, ((ReadOnlySequence<byte>)buf).ToArray());
        }
        finally
        {
            buf.TryRelease();
        }
    }

    /// <summary>
    /// 验证对已链接段再次 <see cref="SequenceBuffer{T}.Append(SequenceBufferSegment{T})"/> 时抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    [Fact]
    public void SequenceBuffer_Append_AlreadyLinkedSegment_Throws()
    {
        var segProv = SequenceBufferSegmentProvider<byte>.Shared;
        var block1 = MemoryBlock<byte>.GetBuffer(new byte[] { 1 });
        var block2 = MemoryBlock<byte>.GetBuffer(new byte[] { 2 });
        var s1 = segProv.GetSegment(block1);
        var s2 = segProv.GetSegment(block2);

        var buf = SequenceBuffer<byte>.GetBuffer();
        try
        {
            buf.Append(s1);
            buf.Append(s2);
            Assert.Throws<InvalidOperationException>(() => buf.Append(s1));
        }
        finally
        {
            buf.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="SequenceBuffer{T}.InsertAtPosition(long,SequenceBufferSegment{T})"/> 在首部插入后序列与长度正确。
    /// </summary>
    [Fact]
    public void SequenceBuffer_InsertAtPosition_Head_PrependsBytes()
    {
        var segProv = SequenceBufferSegmentProvider<byte>.Shared;
        var insertBlock = MemoryBlock<byte>.GetBuffer(new byte[] { 9, 8 });
        var insertSeg = segProv.GetSegment(insertBlock);

        var buf = SequenceBuffer<byte>.GetBuffer();
        try
        {
            buf.Write(new byte[] { 1, 2, 3 });
            buf.InsertAtPosition(0, insertSeg);
            Assert.Equal(5, buf.Committed);
            Assert.Equal(new byte[] { 9, 8, 1, 2, 3 }, ((ReadOnlySequence<byte>)buf).ToArray());
        }
        finally
        {
            buf.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="SequenceBuffer{T}.TryGetSegmentByPosition(long,out SequenceBufferSegment{T},out int)"/> 在合法位置返回 true 且偏移正确。
    /// </summary>
    [Fact]
    public void SequenceBuffer_TryGetSegmentByPosition_FindsSegmentAndOffset()
    {
        var buf = SequenceBuffer<byte>.GetBuffer();
        try
        {
            buf.Write(new byte[] { 10, 20 });
            buf.Write(new byte[] { 30 });

            Assert.True(buf.TryGetSegmentByPosition(0, out _, out var o0));
            Assert.Equal(0, o0);

            Assert.True(buf.TryGetSegmentByPosition(1, out _, out var o1));
            Assert.Equal(1, o1);

            Assert.True(buf.TryGetSegmentByPosition(2, out _, out var o2));
            Assert.Equal(0, o2);

            Assert.False(buf.TryGetSegmentByPosition(3, out _, out _));
        }
        finally
        {
            buf.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="SequenceBufferSegment{T}.Slice(int,int)"/> 在已提交数据上得到对应子范围。
    /// </summary>
    [Fact]
    public void SequenceBufferSegment_Slice_ReturnsSubRangeSegment()
    {
        var segProv = SequenceBufferSegmentProvider<byte>.Shared;
        var block = MemoryBlock<byte>.GetBuffer(new byte[] { 0x10, 0x20, 0x30, 0x40 });
        var seg = segProv.GetSegment(block);
        var sliced = seg.Slice(1, 2);
        var buf = SequenceBuffer<byte>.GetBuffer();
        try
        {
            buf.Append(sliced);
            Assert.Equal(new byte[] { 0x20, 0x30 }, ((ReadOnlySequence<byte>)buf).ToArray());
        }
        finally
        {
            buf.TryRelease();
            seg.Release();
        }
    }

    /// <summary>
    /// 验证 <see cref="SequenceBufferSegment{T}.Clone"/> 得到独立段且已提交内容一致。
    /// </summary>
    [Fact]
    public void SequenceBufferSegment_Clone_MatchesCommitted()
    {
        var segProv = SequenceBufferSegmentProvider<byte>.Shared;
        var block = MemoryBlock<byte>.GetBuffer(new byte[] { 5, 6 });
        var seg = segProv.GetSegment(block);
        var clone = seg.Clone();
        var bufA = SequenceBuffer<byte>.GetBuffer();
        var bufB = SequenceBuffer<byte>.GetBuffer();
        try
        {
            bufA.Append(seg);
            bufB.Append(clone);
            Assert.Equal(((ReadOnlySequence<byte>)bufA).ToArray(), ((ReadOnlySequence<byte>)bufB).ToArray());
        }
        finally
        {
            bufA.TryRelease();
            bufB.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="SequenceBufferSegment{T}.GetEnumerator"/> 沿链表遍历所有段。
    /// </summary>
    [Fact]
    public void SequenceBufferSegment_GetEnumerator_WalksChain()
    {
        var segProv = SequenceBufferSegmentProvider<byte>.Shared;
        var s1 = segProv.GetSegment(MemoryBlock<byte>.GetBuffer(new byte[] { 1 }));
        var s2 = segProv.GetSegment(MemoryBlock<byte>.GetBuffer(new byte[] { 2, 3 }));

        var buf = SequenceBuffer<byte>.GetBuffer();
        try
        {
            buf.Append(s1);
            buf.Append(s2);

            var n = 0;
            foreach (var _ in s1)
                n++;

            Assert.Equal(2, n);
            Assert.Equal(2, s1.Count);
        }
        finally
        {
            buf.TryRelease();
        }
    }

    /// <summary>
    /// 验证 <see cref="ByteBlock.IMemoryOwner"/> 构造可读写字节且 <see cref="ByteBlock.TryRelease"/> 不抛异常。
    /// </summary>
    [Fact]
    public void ByteBlock_FromMemoryOwner_WritesAndDisposes()
    {
        using var owner = MemoryPool<byte>.Shared.Rent(4);
        owner.Memory.Span.Fill(0xCD);

        var block = new ByteBlock(owner);
        try
        {
            block.GetSpan(2)[0] = 0x11;
            block.GetSpan(2)[1] = 0x22;
            block.Advance(2);
            Assert.Equal(2, block.Committed);
            Assert.Equal(0x11, block.CommittedSpan[0]);
            Assert.Equal(0x22, block.CommittedSpan[1]);
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// 验证 <see cref="ByteBlock.Write(NativeByteMemory)"/> 将原生内存内容追加到已提交区。
    /// </summary>
    [Fact]
    public void ByteBlock_Write_NativeByteMemory_AppendsSpan()
    {
        var src = new byte[] { 1, 2, 3 };
        using var native = new NativeByteMemory(src);

        var block = new ByteBlock(8);
        try
        {
            block.Write(native);
            Assert.Equal(3, block.Committed);
            Assert.Equal(src, block.CommittedSpan.ToArray());
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// 用于 <see cref="ObjectPool.Create{T}(PooledObjectPolicy{T},ObjectPoolProvider?,int)"/> 路径的唯一引用类型。
    /// </summary>
    private sealed class PolicyCreateSample
    {
        public int Id { get; set; }
    }

    /// <summary>
    /// 验证 <see cref="ObjectPool.Create{T}(PooledObjectPolicy{T},ObjectPoolProvider?,int)"/> 使用默认策略时可租还实例。
    /// </summary>
    [Fact]
    public void ObjectPool_Create_WithPooledObjectPolicy_GetAndRelease()
    {
        var pool = ObjectPool.Create(new DefaultPooledObjectPolicy<PolicyCreateSample>());
        var a = pool.Get();
        a.Id = 42;
        pool.Release(a);
        var b = pool.Get();
        Assert.Equal(42, b.Id);
    }

    /// <summary>
    /// 验证自定义 <see cref="MemoryPool{T}"/> 的 <see cref="MemoryPoolBlockProvider{T}"/> 可租用与归还块。
    /// </summary>
    [Fact]
    public void MemoryPoolBlockProvider_CustomMemoryPool_GetBuffer_Release()
    {
        var provider = new MemoryPoolBlockProvider<byte>(MemoryPool<byte>.Shared);
        var block = provider.GetBuffer(16);
        try
        {
            block.Write(new byte[] { 1, 2 });
            Assert.Equal(2, block.Committed);
        }
        finally
        {
            provider.Release(block);
        }
    }

    /// <summary>
    /// 验证 <see cref="BinaryReaderAdapter"/> 在多段 <see cref="ReadOnlySequence{T}"/> 上顺序读完负载。
    /// </summary>
    [Fact]
    public void BinaryReaderAdapter_MultiSegment_ReadsAllBytes()
    {
        var c0 = new BufferChunk(new byte[] { 1, 2 });
        var c1 = c0.LinkTo(new BufferChunk(new byte[] { 3 }));
        var seq = new ReadOnlySequence<byte>(c0, 0, c1, c1.Memory.Length);

        var adapter = new BinaryReaderAdapter(seq);
        Span<byte> dest = stackalloc byte[3];
        Assert.True(adapter.TryRead(dest));
        Assert.Equal(new byte[] { 1, 2, 3 }, dest.ToArray());
        Assert.True(adapter.End);
    }
}
