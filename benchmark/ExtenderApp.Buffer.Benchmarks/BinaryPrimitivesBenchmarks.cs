using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace ExtenderApp.Buffer.Benchmarks;

/// <summary>
/// 批量写入 32 位整数：<see cref="ByteBlockExtensions.Write{T}"/> 与 <see cref="BinaryPrimitivesExtensions"/>、<see cref="BinaryPrimitives"/> 对比。
/// </summary>
[MemoryDiagnoser]
public class BinaryPrimitivesBenchmarks
{
    [Params(512, 4096)]
    public int IntCount { get; set; }

    private byte[] _buffer = Array.Empty<byte>();

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new byte[IntCount * 4];
    }

    /// <summary>
    /// 通过 <see cref="ByteBlock"/> 扩展写入 <see cref="int"/>（大端）。
    /// </summary>
    [Benchmark]
    public int ByteBlock_WriteInt32_Extensions()
    {
        var block = new ByteBlock(IntCount * 4);
        try
        {
            for (int i = 0; i < IntCount; i++)
            {
                block.Write(i, isBigEndian: true);
            }

            return block.Committed;
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>
    /// 在连续数组跨度上使用 <see cref="BinaryPrimitivesExtensions.WriteInt32"/>。
    /// </summary>
    [Benchmark(Baseline = true)]
    public int Span_WriteInt32_Extensions()
    {
        Span<byte> span = _buffer;
        int offset = 0;
        for (int i = 0; i < IntCount; i++)
        {
            span.Slice(offset, 4).WriteInt32(i, isBigEndian: true);
            offset += 4;
        }

        return offset;
    }

    /// <summary>
    /// 直接使用 <see cref="BinaryPrimitives.WriteInt32BigEndian"/>。
    /// </summary>
    [Benchmark]
    public int Span_WriteInt32_BinaryPrimitives()
    {
        Span<byte> span = _buffer;
        int offset = 0;
        for (int i = 0; i < IntCount; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(offset, 4), i);
            offset += 4;
        }

        return offset;
    }
}
