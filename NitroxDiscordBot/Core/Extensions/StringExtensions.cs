using System.Globalization;

namespace NitroxDiscordBot.Core.Extensions;

public static class StringExtensions
{
    private static readonly char[] sentenceSplitCharacters = ['.', '!', '?', '"', '`', ':'];

    public static uint ParseHexToUint(this string hex)
    {
        uint.TryParse(hex.AsSpan().TrimStart('#'), NumberStyles.HexNumber, null, out uint number);
        return number;
    }

    /// <summary>
    ///     Counts the occurrences of the <paramref name="characters" /> in the <paramref name="text" />.
    /// </summary>
    /// <returns>Sum of the occurrences found in the text.</returns>
    public static int Count(this ReadOnlySpan<char> text, char[] characters)
    {
        int result = 0;
        foreach (char c in characters)
        {
            result += text.Count(c);
        }
        return result;
    }

    /// <summary>
    ///     Tests that any sentences in the given text has a complete match with at least one word group, and where each word
    ///     in the word group follows the same order as in the matched sentence.
    /// </summary>
    public static bool ContainsSentenceWithWordOrderOfAny(this ReadOnlySpan<char> text,
        string[] wordGroupGroups,
        StringComparison comparer = StringComparison.InvariantCultureIgnoreCase)
    {
        Span<Range> sentenceRanges = stackalloc Range[text.Count(sentenceSplitCharacters) + 1];
        text.SplitAny(sentenceRanges, sentenceSplitCharacters,
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (Range sentenceRange in sentenceRanges)
        {
            foreach (string wordGroup in wordGroupGroups)
            {
                if (ContainsWordsInOrder(text[sentenceRange], wordGroup, comparer))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static bool ContainsWordsInOrder(this ReadOnlySpan<char> content,
        ReadOnlySpan<char> words,
        StringComparison comparison = StringComparison.InvariantCultureIgnoreCase)
    {
        static bool IsWordBoundary(char boundary)
        {
            return char.IsWhiteSpace(boundary) || char.IsPunctuation(boundary);
        }

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
                i--;
                goto ignoreResultMoveNext;
            }
            int endOfMatchedWordIndex = index + words[wordRange].Length;
            if (endOfMatchedWordIndex < content.Length && content[endOfMatchedWordIndex] is var end &&
                !IsWordBoundary(end))
            {
                i--;
                goto ignoreResultMoveNext;
            }

            indices[i] = index;
            ignoreResultMoveNext:
            contentSliceStart = index + 1;
        }

        return true;
    }

    public static ArraySegment<TResult> OfParsable<TResult>(this string[] source)
        where TResult : ISpanParsable<TResult>
    {
        TResult[] result = new TResult[source.Length];
        int endOffset = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (TResult.TryParse(source[i], CultureInfo.InvariantCulture, out TResult parsed))
            {
                result[i - endOffset] = parsed;
            }
            else
            {
                endOffset++;
            }
        }
        return new ArraySegment<TResult>(result, 0, result.Length - endOffset);
    }

    public static void OfParsable<TResult>(this string[] source, ref Span<TResult> destination)
        where TResult : ISpanParsable<TResult>
    {
        int endOffset = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (TResult.TryParse(source[i], CultureInfo.InvariantCulture, out TResult parsed))
            {
                destination[i - endOffset] = parsed;
            }
            else
            {
                endOffset++;
            }
        }
        destination = destination.Slice(0, source.Length - endOffset);
    }

    /// <summary>
    ///     Tests the source string array contains the equivalent of the value, once parsed.
    /// </summary>
    public static bool ContainsParsable<TParsable>(this string[] source, TParsable value)
        where TParsable : IEquatable<TParsable>, ISpanParsable<TParsable>
    {
        foreach (string item in source)
        {
            if (TParsable.TryParse(item, CultureInfo.InvariantCulture, out TParsable parsedItem))
            {
                if (parsedItem.Equals(value))
                {
                    return true;
                }
            }
        }
        return false;
    }
}