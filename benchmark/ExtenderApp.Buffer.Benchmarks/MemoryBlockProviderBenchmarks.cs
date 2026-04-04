using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using ExtenderApp.Buffer.MemoryBlocks;

namespace ExtenderApp.Buffer.Benchmarks;

/// <summary>
/// <see cref="MemoryBlock{T}.GetBuffer(int)"/>（数组池路径）、<see cref="MemoryBlock{T}.GetBuffer(T[])"/>（固定数组包装）、
/// 与 <see cref="MemoryPoolBlockProvider{T}"/> 对比，并以 <see cref="ArrayPool{T}"/> 手写写入为基线。
/// 全量参数较慢时可使用 <c>--filter *MemoryBlockProvider*</c> 或缩小 <see cref="TotalBytes"/> 组合。
/// </summary>
[MemoryDiagnoser]
public class MemoryBlockProviderBenchmarks
{
    [Params(256, 4096, 65536, 1048576)]
    public int TotalBytes { get; set; }

    [Params(16, 256)]
    public int ChunkSize { get; set; }

    private byte[] _chunk = Array.Empty<byte>();
    private byte[] _backing = Array.Empty<byte>();

    [GlobalSetup]
    public void Setup()
    {
        _chunk = new byte[ChunkSize];
        _chunk.AsSpan().Fill(0xCD);
        _backing = new byte[TotalBytes];
    }

    /// <summary>
    /// 默认 <see cref="MemoryBlock{T}.GetBuffer(int)"/>（内部为数组池提供者）。
    /// </summary>
    [Benchmark]
    public void ArrayPoolBacked_GetBuffer_Write_Release()
    {
        var block = MemoryBlock<byte>.GetBuffer(TotalBytes);
        try
        {
            int remaining = TotalBytes;
            while (remaining > 0)
            {
                int n = Math.Min(ChunkSize, remaining);
                _chunk.AsSpan(0, n).CopyTo(block.GetSpan(n));
                block.Advance(n);
                remaining -= n;
            }
        }
        finally
        {
            block.TryRelease();
        }
    }

    /// <summary>
    /// 调用方预分配数组后经 <see cref="MemoryBlock{T}.GetBuffer(T[])"/> 包装（固定数组提供者路径），再分块写入。
    /// 说明：<see cref="MemoryBlock{T}.GetBuffer(T[])"/> 将整段视为已提交内容（与「数组内已有数据」视图一致），
    /// 故在测量「向空缓冲分块写入」前需 <see cref="MemoryBlock{T}.Rewind(int)"/> 将写边界归零以腾出可写区。
    /// </summary>
    [Benchmark]
    public void FixedArrayBacked_GetBuffer_Write_Release()
    {
        var block = MemoryBlock<byte>.GetBuffer(_backing);
        try
        {
            block.Rewind((int)block.Committed);
            int remaining = TotalBytes;
            while (remaining > 0)
            {
                int n = Math.Min(ChunkSize, remaining);
                _chunk.AsSpan(0, n).CopyTo(block.GetSpan(n));
                block.Advance(n);
                remaining -= n;
            }
        }
        finally
        {
            block.TryRelease();
        }
    }

    /// <summary>
    /// <see cref="MemoryPoolBlockProvider{T}"/> 租还路径。
    /// </summary>
    [Benchmark]
    public void MemoryPoolProvider_GetBuffer_Write_Release()
    {
        var provider = MemoryPoolBlockProvider<byte>.Default;
        var block = provider.GetBuffer(TotalBytes);
        try
        {
            int remaining = TotalBytes;
            while (remaining > 0)
            {
                int n = Math.Min(ChunkSize, remaining);
                _chunk.AsSpan(0, n).CopyTo(block.GetSpan(n));
                block.Advance(n);
                remaining -= n;
            }
        }
        finally
        {
            provider.Release(block);
        }
    }

    /// <summary>
    /// 基线：租用共享数组池并在连续跨度上分块复制写入后归还。
    /// </summary>
    [Benchmark(Baseline = true)]
    public void ArrayPool_Rent_Copy_Return()
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(TotalBytes);
        try
        {
            Span<byte> span = rented.AsSpan(0, TotalBytes);
            int remaining = TotalBytes;
            int offset = 0;
            while (remaining > 0)
            {
                int n = Math.Min(ChunkSize, remaining);
                _chunk.AsSpan(0, n).CopyTo(span.Slice(offset, n));
                offset += n;
                remaining -= n;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
