using System.Globalization;

namespace NitroxDiscordBot.Core.Extensions;

public static class StringExtensions
{
    public static uint ParseHexToUint(this string hex)
    {
        uint.TryParse(hex.AsSpan().TrimStart('#'), NumberStyles.HexNumber, null, out uint number);
        return number;
    }

    public static bool ContainsWordsInOrder(this ReadOnlySpan<char> content, ReadOnlySpan<char> words, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
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
        words.Split(wordRanges, ' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        int contentSliceStart = 0;
        Span<int> indices = stackalloc int[wordRanges.Length];
        for (int i = 0; i < wordRanges.Length; i++)
        {
            Range wordRange = wordRanges[i];
            int index = contentSliceStart + content.Slice(contentSliceStart).IndexOf(words[wordRange], comparison);
            if (index <= -1) return false;
            // Previous word should be somewhere before the current word.
            if (i > 0 && indices[i - 1] >= index) return false;
            // Test that index is at the start/end of a word (i.e. not somewhere inside it).
            if (index - 1 > -1 && content[index - 1] is var start && !IsWordBoundary(start))
            {
                return false;
            }
            int endOfMatchedWordIndex = index + words[wordRange].Length;
            if (endOfMatchedWordIndex < content.Length && content[endOfMatchedWordIndex] is var end && !IsWordBoundary(end))
            {
                return false;
            }

            indices[i] = index;
            contentSliceStart = index + 1;
        }

        return true;
    }
}