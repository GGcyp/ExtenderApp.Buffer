using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using ExtenderApp.Buffer.Streams;

namespace ExtenderApp.Buffer.Benchmarks;

/// <summary>
/// <see cref="AbstractBufferStreamDecorator"/> 与 <see cref="MemoryStream"/> 的分块读写对比。
/// </summary>
[MemoryDiagnoser]
public class StreamDecoratorBenchmarks
{
    [Params(4096, 65536)]
    public int TotalBytes { get; set; }

    [Params(128, 256)]
    public int ChunkSize { get; set; }

    private byte[] _chunk = Array.Empty<byte>();
    private byte[] _readSource = Array.Empty<byte>();

    [GlobalSetup]
    public void Setup()
    {
        _chunk = new byte[ChunkSize];
        _chunk.AsSpan().Fill(0x5A);
        _readSource = new byte[TotalBytes];
        _readSource.AsSpan().Fill(0x3C);
    }

    /// <summary>
    /// 向 <see cref="AbstractBufferStreamDecorator"/> 分块写入。
    /// </summary>
    [Benchmark]
    public void Decorator_Write_Chunks()
    {
        using var stream = new AbstractBufferStreamDecorator();
        int remaining = TotalBytes;
        while (remaining > 0)
        {
            int n = Math.Min(ChunkSize, remaining);
            stream.Write(_chunk, 0, n);
            remaining -= n;
        }
    }

    /// <summary>
    /// 基线：<see cref="MemoryStream"/> 分块写入。
    /// </summary>
    [Benchmark(Baseline = true)]
    public void MemoryStream_Write_Chunks()
    {
        using var stream = new MemoryStream(TotalBytes);
        int remaining = TotalBytes;
        while (remaining > 0)
        {
            int n = Math.Min(ChunkSize, remaining);
            stream.Write(_chunk, 0, n);
            remaining -= n;
        }
    }

    /// <summary>
    /// 从装饰器流分块读出（预先绑定含数据的 <see cref="MemoryBlock{byte}"/>）。
    /// </summary>
    [Benchmark]
    public int Decorator_Read_Chunks()
    {
        var block = MemoryBlock<byte>.GetBuffer(_readSource.AsSpan());
        try
        {
            using var stream = new AbstractBufferStreamDecorator();
            stream.SetReadBuffer(block);
            var scratch = new byte[ChunkSize];
            int total = 0;
            int read;
            while ((read = stream.Read(scratch, 0, scratch.Length)) > 0)
            {
                total += read;
            }

            return total;
        }
        finally
        {
            block.TryRelease();
        }
    }

    /// <summary>
    /// 基线：<see cref="MemoryStream"/> 分块读出。
    /// </summary>
    [Benchmark]
    public int MemoryStream_Read_Chunks()
    {
        using var stream = new MemoryStream(_readSource, writable: false);
        var scratch = new byte[ChunkSize];
        int total = 0;
        int read;
        while ((read = stream.Read(scratch, 0, scratch.Length)) > 0)
        {
            total += read;
        }

        return total;
    }
}
