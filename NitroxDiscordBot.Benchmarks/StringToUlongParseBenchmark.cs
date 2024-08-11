using BenchmarkDotNet.Attributes;

namespace NitroxDiscordBot.Benchmarks;

[MemoryDiagnoser]
public class StringToUlongParseBenchmark
{
    private static readonly string[] data = [106248L.ToString(), 102510124L.ToString(), "invalid", 7236202510124L.ToString()];

    [Benchmark(Baseline = true)]
    public int For()
    {
        return data.OfParsable<ulong>().Count;
    }

    [Benchmark]
    public int Span()
    {
        Span<ulong> result = stackalloc ulong[data.Length];
        data.OfParsable(ref result);
        return result.Length;
    }
}