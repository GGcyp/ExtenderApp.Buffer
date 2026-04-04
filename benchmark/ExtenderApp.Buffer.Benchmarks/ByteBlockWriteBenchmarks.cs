using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace ExtenderApp.Buffer.Benchmarks;

/// <summary>
/// <see cref="ByteBlock"/> 写入路径与 <see cref="MemoryStream"/> 基线对比。
/// 参数组合较多时可用 <c>--filter *ByteBlock*</c> 缩小范围。
/// </summary>
[MemoryDiagnoser]
public class ByteBlockWriteBenchmarks
{
    [Params(256, 4096, 65536, 1048576)]
    public int TotalBytes { get; set; }

    [Params(16, 64, 256)]
    public int ChunkSize { get; set; }

    private byte[] _chunk = Array.Empty<byte>();

    [GlobalSetup]
    public void Setup()
    {
        _chunk = new byte[ChunkSize];
        _chunk.AsSpan().Fill(0xAB);
    }

    /// <summary>
    /// 通过 <see cref="ByteBlock.GetSpan"/> / <see cref="ByteBlock.Advance"/> 分块写入。
    /// </summary>
    [Benchmark]
    public void ByteBlock_GetSpanAdvance()
    {
        using var block = new ByteBlock(TotalBytes);
        int remaining = TotalBytes;
        while (remaining > 0)
        {
            int n = Math.Min(ChunkSize, remaining);
            _chunk.AsSpan(0, n).CopyTo(block.GetSpan(n));
            block.Advance(n);
            remaining -= n;
        }
    }

    /// <summary>
    /// 通过 <see cref="ByteBlock.Write(ReadOnlySpan{byte})"/> 分块写入。
    /// </summary>
    [Benchmark]
    public void ByteBlock_WriteSpan()
    {
        using var block = new ByteBlock(TotalBytes);
        int remaining = TotalBytes;
        while (remaining > 0)
        {
            int n = Math.Min(ChunkSize, remaining);
            block.Write(_chunk.AsSpan(0, n));
            remaining -= n;
        }
    }

    /// <summary>
    /// 通过 <see cref="ByteBlock.Write(byte[], int, int)"/> 分块写入。
    /// </summary>
    [Benchmark]
    public void ByteBlock_WriteByteArray()
    {
        using var block = new ByteBlock(TotalBytes);
        int remaining = TotalBytes;
        while (remaining > 0)
        {
            int n = Math.Min(ChunkSize, remaining);
            block.Write(_chunk, 0, n);
            remaining -= n;
        }
    }

    /// <summary>
    /// 基线：<see cref="MemoryStream"/> 分块写入。
    /// </summary>
    [Benchmark(Baseline = true)]
    public void MemoryStream_WriteSpan()
    {
        using var ms = new MemoryStream(TotalBytes);
        int remaining = TotalBytes;
        while (remaining > 0)
        {
            int n = Math.Min(ChunkSize, remaining);
            ms.Write(_chunk, 0, n);
            remaining -= n;
        }
    }
}
