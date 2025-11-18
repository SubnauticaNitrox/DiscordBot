using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;

namespace NitroxDiscordBot.Benchmarks;

[MemoryDiagnoser]
public partial class ContainsSentenceWithWordOrderOfAnyBenchmark
{
    private const string RegexPattern = @"^.*\b(play)\b[^.!?:;`\n]*\b(game|subnautica)\b[^.!?:;`\n]*\b(me)\b";

    private const RegexOptions RegexDefaultOptions =
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking;

    private const string Text = "Can anyone help me? I would like to play the game subnautica, anyone with me?";

    private static Regex interpretedRegex = null!;
    private static Regex compiledRegex = null!;

    [GeneratedRegex(RegexPattern, RegexDefaultOptions)]
    private static partial Regex Regex();

    [GlobalSetup]
    public void Setup()
    {
        interpretedRegex = new Regex(RegexPattern, RegexDefaultOptions);
        compiledRegex = new Regex(RegexPattern, RegexDefaultOptions | RegexOptions.Compiled);
    }

    [Benchmark(Baseline = true)]
    public bool Interpreted()
    {
        return interpretedRegex.IsMatch(Text);
    }

    [Benchmark]
    public bool Compiled()
    {
        return compiledRegex.IsMatch(Text);
    }

    [Benchmark]
    public bool SourceGen()
    {
        return Regex().IsMatch(Text);
    }
}