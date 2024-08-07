using System.Globalization;

namespace NitroxDiscordBot.Core.Extensions;

public static class EnumerableExtensions
{
    /// <summary>
    ///     Tries to parse every item in the source. If the parse fails, the item is skipped.
    /// </summary>
    public static IEnumerable<TResult> OfParsable<TResult>(this IEnumerable<string> source)
        where TResult : ISpanParsable<TResult>
    {
        static bool TryParse(string text, out TResult result)
        {
            if (TResult.TryParse(text, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }
            result = default;
            return false;
        }

        foreach (string item in source)
        {
            if (TryParse(item, out TResult result))
            {
                yield return result;
            }
        }
    }
}