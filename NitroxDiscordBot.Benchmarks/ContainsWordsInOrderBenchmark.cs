using BenchmarkDotNet.Attributes;

namespace NitroxDiscordBot.Benchmarks;

[MemoryDiagnoser]
public class ContainsWordsInOrderBenchmark
{
    [Benchmark(Baseline = true)]
    public void OrdinalIgnoreCase()
    {
        "Can you tell me the way to the nearest hospital!?".AsSpan()
            .ContainsWordsInOrder("tell hospital nearest", StringComparison.OrdinalIgnoreCase);
    }

    [Benchmark]
    public void InvariantIgnoreCase()
    {
        "Can you tell me the way to the nearest hospital!?".AsSpan().ContainsWordsInOrder("tell hospital nearest");
    }
}