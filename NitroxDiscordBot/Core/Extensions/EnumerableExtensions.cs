namespace NitroxDiscordBot.Core.Extensions;

public static class EnumerableExtensions
{
    public delegate bool TryFunc<in TSource, TResult>(TSource arg, out TResult result);

    public static IEnumerable<TResult> WhereTryParse<TSource, TResult>(this IEnumerable<TSource> source,
        TryFunc<TSource, TResult> selector)
    {
        foreach (TSource item in source)
        {
            if (selector(item, out TResult r))
            {
                yield return r;
            }
        }
    }
}