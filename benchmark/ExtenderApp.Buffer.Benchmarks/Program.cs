using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace ExtenderApp.Buffer.Benchmarks;

/// <summary>
/// 性能基准入口。请在 Release 配置下运行以获得可信计时：
/// <c>dotnet run -c Release --project benchmark/ExtenderApp.Buffer.Benchmarks</c>；
/// 可用 <c>--filter</c> 限定类型或方法，例如 <c>--filter *ByteBlock*</c>。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 默认 Job：缩短暖机与实测迭代次数，加快本地运行；统计稳定性弱于 BDN 全量默认，正式对比可临时改用命令行 <c>--job</c> 或移除本配置。
    /// </summary>
    private static readonly Job FastLocalJob = Job.Default
        .WithWarmupCount(5)
        .WithMaxWarmupCount(12)
        .WithIterationCount(15)
        .WithMaxIterationCount(20);

    private static void Main(string[] args)
    {
        IConfig config = ManualConfig.CreateMinimumViable()
            .AddExporter(MarkdownExporter.Default)
            .AddExporter(HtmlExporter.Default)
            .AddJob(FastLocalJob);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
