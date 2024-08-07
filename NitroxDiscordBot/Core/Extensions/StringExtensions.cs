using System.Globalization;

namespace NitroxDiscordBot.Core.Extensions;

public static class StringExtensions
{
    public static uint ParseHexToUint(this string hex)
    {
        uint.TryParse(hex.AsSpan().TrimStart('#'), NumberStyles.HexNumber, null, out var number);
        return number;
    }

    public static bool ContainsWordsInOrder(this ReadOnlySpan<char> content, ReadOnlySpan<char> words)
    {
        static bool IsWordBoundary(char boundary) => char.IsWhiteSpace(boundary) || char.IsPunctuation(boundary);

        if (content.IsEmpty)
        {
            return words.Trim().IsEmpty;
        }
        if (words.Trim().IsEmpty)
        {
            return true;
        }
        Span<Range> wordRanges = stackalloc Range[words.Trim().Count(' ') + 1];
        words.Split(wordRanges, " ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        Span<int> indices = stackalloc int[wordRanges.Length];
        for (int i = 0; i < wordRanges.Length; i++)
        {
            Range wordRange = wordRanges[i];
            indices[i] = content.IndexOf(words[wordRange], StringComparison.InvariantCultureIgnoreCase);
            if (indices[i] <= -1) return false;
            // Test that index at the start/end of a word (not as part of a word).
            if (indices[i] - 1 > -1 && content[indices[i] - 1] is var start && !IsWordBoundary(start))
            {
                return false;
            }
            int endOfMatchedWordIndex = indices[i] + words[wordRange].Length;
            if (endOfMatchedWordIndex < content.Length && content[endOfMatchedWordIndex] is var end && !IsWordBoundary(end))
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