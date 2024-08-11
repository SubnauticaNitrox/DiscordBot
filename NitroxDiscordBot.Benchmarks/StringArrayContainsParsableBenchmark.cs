using BenchmarkDotNet.Attributes;

namespace NitroxDiscordBot.Benchmarks;

[MemoryDiagnoser]
public class StringArrayContainsParsableBenchmark
{
    private static readonly string[] data = [106248L.ToString(), 102510124L.ToString(), "invalid", 7236202510124L.ToString()];

    [Benchmark(Baseline = true)]
    public bool Ulong()
    {
        return data.ContainsParsable(7236202510124L);
    }

    [Benchmark]
    public bool String()
    {
        return data.Contains(7236202510124L.ToString(), StringComparer.OrdinalIgnoreCase);
    }
}