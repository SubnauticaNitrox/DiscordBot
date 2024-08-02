using System.Globalization;

namespace NitroxDiscordBot.Core.Extensions;

public static class StringExtensions
{
    public static uint ParseHexToUint(this string hex)
    {
        uint.TryParse(hex.AsSpan().TrimStart('#'), NumberStyles.HexNumber, null, out var number);
        return number;
    }

    public static bool ContainsWordsInOrder(this string content, ReadOnlySpan<char> words) => content.AsSpan().ContainsWordsInOrder(words);

    public static bool ContainsWordsInOrder(this ReadOnlySpan<char> content, ReadOnlySpan<char> words)
    {
        if (content.IsEmpty)
        {
            return false;
        }
        if (words.Trim().IsEmpty)
        {
            return false;
        }
        Span<Range> wordRanges = new Range[words.Trim().Count(' ') + 1];
        words.Split(wordRanges, " ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        Span<int> indices = stackalloc int[wordRanges.Length];
        for (int i = 0; i < wordRanges.Length; i++)
        {
            Range wordRange = wordRanges[i];
            indices[i] = content.IndexOf(words[wordRange], StringComparison.InvariantCultureIgnoreCase);
            if (indices[i] <= -1) return false;
            // Test that index at the start/end of a word (not as part of a word).
            if ((indices[i] - 1 > -1 && content[indices[i] - 1] != ' ') || (indices[i] + words[wordRange].Length < content.Length && content[indices[i] + words[wordRange].Length] != ' '))
            {
                return false;
            }
        }

        // Test order of words as expected.
        int? lastIndex = null;
        foreach (int index in indices)
        {
            if (!lastIndex.HasValue)
            {
                lastIndex = index;
                continue;
            }
            if (index < lastIndex)
            {
                return false;
            }

            lastIndex = index;
        }

        return true;
    }
}