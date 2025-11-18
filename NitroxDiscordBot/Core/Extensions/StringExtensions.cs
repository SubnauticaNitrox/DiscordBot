using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NitroxDiscordBot.Core.Extensions;

public static partial class StringExtensions
{
    private const string RegexSentenceSeparatorCharacters = @"[^.!?:;`\n]";
    [GeneratedRegex(@"^[a-z0-9]+(?:\|[a-z0-9]+)*$", RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex ValidWordPatternRegex();

    public static uint ParseHexToUint(this string hex)
    {
        uint.TryParse(hex.AsSpan().TrimStart('#'), NumberStyles.HexNumber, null, out uint number);
        return number;
    }

    extension(string[] wordGroups)
    {
        public Regex[] CreateRegexesForAnyWordGroupInOrderInSentence()
        {
            Regex[] result = new Regex[wordGroups.Length];

            StringBuilder regexBuilder = new();
            for (int i = 0; i < wordGroups.Length; i++)
            {
                regexBuilder.Append("^.*");

                string[] patternGroups = wordGroups[i].Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (patternGroups.Length < 1)
                {
                    throw new Exception("There must be at least one word group");
                }
                foreach (string wordPattern in patternGroups)
                {
                    if (!ValidWordPatternRegex().IsMatch(wordPattern))
                    {
                        throw new Exception($"Invalid word pattern '{wordPattern}'");
                    }

                    regexBuilder.Append(@"\b(")
                        .Append(wordPattern)
                        .Append(@")\b")
                        .Append(RegexSentenceSeparatorCharacters)
                        .Append('*');
                }
                regexBuilder.Remove(regexBuilder.Length - RegexSentenceSeparatorCharacters.Length - 1, RegexSentenceSeparatorCharacters.Length + 1);
                result[i] = new Regex(regexBuilder.ToString(),
                    RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking |
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);
                regexBuilder.Clear();
            }

            return result;
        }

        public ArraySegment<TResult> OfParsable<TResult>()
            where TResult : ISpanParsable<TResult>
        {
            TResult[] result = new TResult[wordGroups.Length];
            int endOffset = 0;
            for (int i = 0; i < wordGroups.Length; i++)
            {
                if (TResult.TryParse(wordGroups[i], CultureInfo.InvariantCulture, out TResult? parsed))
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

        public void OfParsable<TResult>(ref Span<TResult> destination)
            where TResult : ISpanParsable<TResult>
        {
            int endOffset = 0;
            for (int i = 0; i < wordGroups.Length; i++)
            {
                if (TResult.TryParse(wordGroups[i], CultureInfo.InvariantCulture, out TResult? parsed))
                {
                    destination[i - endOffset] = parsed;
                }
                else
                {
                    endOffset++;
                }
            }
            destination = destination.Slice(0, wordGroups.Length - endOffset);
        }

        /// <summary>
        ///     Tests the source string array contains the equivalent of the value, once parsed.
        /// </summary>
        public bool ContainsParsable<TParsable>(TParsable value)
            where TParsable : IEquatable<TParsable>, ISpanParsable<TParsable>
        {
            foreach (string item in wordGroups)
            {
                if (TParsable.TryParse(item, CultureInfo.InvariantCulture, out TParsable? parsedItem))
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

    extension(string? value)
    {
        public string Limit(int limit, string postfix = "")
        {
            if (value == null || limit <= 0)
            {
                return "";
            }
            postfix ??= "";
            if (value.Length + postfix.Length <= limit)
            {
                if (postfix.Length == 0)
                {
                    return value;
                }
                return value + postfix;
            }
            // Truncate value so it fits within limit when postfix is appended.
            value = value[..Math.Max(0, limit - postfix.Length)];
            // Truncate postfix if it's larger than the limit when <truncated value> + <postfix> (e.g. when value is "1", postfix is "..." and limit is 2)
            postfix = postfix[..Math.Max(0, Math.Min(limit - value.Length, postfix.Length))];
            return value + postfix;
        }
    }
}