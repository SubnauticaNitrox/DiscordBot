using BenchmarkDotNet.Attributes;

namespace NitroxDiscordBot.Benchmarks;

[MemoryDiagnoser]
public class ContainsSentenceWithWordOrderOfAnyBenchmark
{
    private const string Text =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec nec libero at nibh tristique viverra non non felis.";
    private static readonly char[] sentenceSplitCharacters = ['.', '!', '?', '"', '`', ':'];
    private static readonly string[] wordPatterns = ["non non felis"];

    [Benchmark(Baseline = true, Description = "Span")]
    public bool ContainsWordOrderOfAnyInSentenceOrderOfAny_Span()
    {
        return Text.AsSpan().ContainsSentenceWithWordOrderOfAny(wordPatterns);
    }

    [Benchmark(Description = "String")]
    public bool ContainsWordOrderOfAnyInSentenceOrderOfAny_String()
    {
        string[] sentences = Text.Split(sentenceSplitCharacters,
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (string sentence in sentences)
        {
            if (sentence.AsSpan().ContainsWordsInOrder(wordPatterns[0]))
            {
                return true;
            }
        }
        return false;
    }
}