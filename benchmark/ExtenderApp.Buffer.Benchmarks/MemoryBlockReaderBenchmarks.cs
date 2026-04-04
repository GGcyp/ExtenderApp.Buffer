using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using ExtenderApp.Buffer.Reader;

namespace ExtenderApp.Buffer.Benchmarks;

/// <summary>
/// 顺序读取 <see cref="int"/>：<see cref="AbstractBufferReaderExtensions.TryRead{T}"/> 与 <see cref="ReadOnlySpan{byte}.TryRead{T}"/> 扩展对比。
/// </summary>
[MemoryDiagnoser]
public class MemoryBlockReaderBenchmarks
{
    [Params(256, 2048)]
    public int IntCount { get; set; }

    private byte[] _source = Array.Empty<byte>();

    [GlobalSetup]
    public void Setup()
    {
        _source = new byte[IntCount * 4];
        var span = _source.AsSpan();
        for (int i = 0; i < IntCount; i++)
        {
            span.Slice(i * 4, 4).WriteInt32(i, isBigEndian: true);
        }
    }

    /// <summary>
    /// 基线：在只读跨度上重复 <see cref="AbstractBufferExtensions.TryRead{T}"/>。
    /// </summary>
    [Benchmark(Baseline = true)]
    public int Span_TryReadInt32_Loop()
    {
        ReadOnlySpan<byte> span = _source;
        int offset = 0;
        int sum = 0;
        for (int i = 0; i < IntCount; i++)
        {
            if (!span.Slice(offset).TryRead(out int value, out int size, isBigEndian: true))
            {
                throw new InvalidOperationException("读取失败。");
            }

            sum += value;
            offset += size;
        }

        return sum;
    }

    /// <summary>
    /// 从复制的 <see cref="MemoryBlock{byte}"/> 经 <see cref="MemoryBlockReader{T}"/> 消费整段整数。
    /// </summary>
    [Benchmark]
    public int MemoryBlockReader_TryReadInt32_Loop()
    {
        var block = MemoryBlock<byte>.GetBuffer(_source.AsSpan());
        try
        {
            var reader = MemoryBlockReader<byte>.GetReader(block);
            try
            {
                int sum = 0;
                for (int i = 0; i < IntCount; i++)
                {
                    if (!reader.TryRead(out int value, out _, isBigEndian: true))
                    {
                        throw new InvalidOperationException("读取失败。");
                    }

                    sum += value;
                }

                return sum;
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
}
