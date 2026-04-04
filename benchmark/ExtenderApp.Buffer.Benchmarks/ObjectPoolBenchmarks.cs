using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace ExtenderApp.Buffer.Benchmarks;

/// <summary>
/// <see cref="DefaultObjectPool{T}"/>、<see cref="DisposableObjectPool{T}"/> 租还与直接 <c>new</c> 的成本对比。
/// </summary>
[MemoryDiagnoser]
public class ObjectPoolBenchmarks
{
    private DefaultObjectPool<StringBuilder>? _pool;
    private DisposableObjectPool<StringBuilder>? _disposablePool;

    [GlobalSetup]
    public void Setup()
    {
        var policy = new DefaultPooledObjectPolicy<StringBuilder>();
        _pool = new DefaultObjectPool<StringBuilder>(policy, maximumRetained: 128);
        _disposablePool = new DisposableObjectPool<StringBuilder>(policy, maximumRetained: 128);
    }

    /// <summary>
    /// 从池中获取 <see cref="StringBuilder"/>，追加短文本后归还。
    /// </summary>
    [Benchmark(Baseline = true)]
    public int PooledStringBuilder_GetAppendRelease()
    {
        var sb = _pool!.Get();
        sb.Clear();
        sb.Append("bench");
        _pool.Release(sb);
        return sb.Length;
    }

    /// <summary>
    /// 从可释放对象池获取 <see cref="StringBuilder"/>，追加短文本后归还（含 disposed 检查路径）。
    /// </summary>
    [Benchmark]
    public int DisposablePooledStringBuilder_GetAppendRelease()
    {
        var sb = _disposablePool!.Get();
        sb.Clear();
        sb.Append("bench");
        _disposablePool.Release(sb);
        return sb.Length;
    }

    /// <summary>
    /// 每次分配新的 <see cref="StringBuilder"/> 并追加短文本（无归还）。
    /// </summary>
    [Benchmark]
    public int NewStringBuilder_Append()
    {
        var sb = new StringBuilder();
        sb.Append("bench");
        return sb.Length;
    }
}
