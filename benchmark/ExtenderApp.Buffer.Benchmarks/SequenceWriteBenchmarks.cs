using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using ExtenderApp.Buffer.SequenceBuffers;
using ExtenderApp.Buffer.ValueBuffers;

namespace ExtenderApp.Buffer.Benchmarks;

/// <summary>
/// 序列型缓冲分块写入：<see cref="ByteBuffer"/>、<see cref="SequenceBuffer{T}"/>、<see cref="FastSequence{T}"/>、<see cref="ValueSequenceBuffer{T}"/>。
/// 参数组合较多时可用 <c>--filter *SequenceWrite*</c> 缩小范围。
/// </summary>
[MemoryDiagnoser]
public class SequenceWriteBenchmarks
{
    [Params(256, 4096, 65536, 1048576)]
    public int TotalBytes { get; set; }

    [Params(16, 256)]
    public int ChunkSize { get; set; }

    private byte[] _chunk = Array.Empty<byte>();

    [GlobalSetup]
    public void Setup()
    {
        _chunk = new byte[ChunkSize];
        _chunk.AsSpan().Fill(0xEF);
    }

    [Benchmark(Baseline = true)]
    public void ByteBuffer_ChunkedWrite_Dispose()
    {
        var buffer = new ByteBuffer();
        try
        {
            int remaining = TotalBytes;
            while (remaining > 0)
            {
                int n = Math.Min(ChunkSize, remaining);
                buffer.Write(_chunk.AsSpan(0, n));
                remaining -= n;
            }
        }
        finally
        {
            buffer.Dispose();
        }
    }

    [Benchmark]
    public void SequenceBuffer_ChunkedWrite_TryRelease()
    {
        var seq = SequenceBuffer<byte>.GetBuffer();
        try
        {
            int remaining = TotalBytes;
            while (remaining > 0)
            {
                int n = Math.Min(ChunkSize, remaining);
                _chunk.AsSpan(0, n).CopyTo(seq.GetSpan(n));
                seq.Advance(n);
                remaining -= n;
            }
        }
        finally
        {
            seq.TryRelease();
        }
    }

    [Benchmark]
    public void FastSequence_ChunkedWrite_TryRelease()
    {
        var seq = FastSequence<byte>.GetBuffer();
        try
        {
            int remaining = TotalBytes;
            while (remaining > 0)
            {
                int n = Math.Min(ChunkSize, remaining);
                _chunk.AsSpan(0, n).CopyTo(seq.GetSpan(n));
                seq.Advance(n);
                remaining -= n;
            }
        }
        finally
        {
            seq.TryRelease();
        }
    }

    /// <summary>
    /// 经 <see cref="ValueSequenceBuffer{T}"/> 委托至 <see cref="FastSequence{T}"/> 的分块写入，度量包装层开销。
    /// </summary>
    [Benchmark]
    public void ValueSequenceBuffer_ChunkedWrite_Dispose()
    {
        var seq = FastSequence<byte>.GetBuffer();
        var wrapper = new ValueSequenceBuffer<byte>(seq);
        try
        {
            int remaining = TotalBytes;
            while (remaining > 0)
            {
                int n = Math.Min(ChunkSize, remaining);
                _chunk.AsSpan(0, n).CopyTo(wrapper.GetSpan(n));
                wrapper.Advance(n);
                remaining -= n;
            }
        }
        finally
        {
            wrapper.Dispose();
        }
    }
}
