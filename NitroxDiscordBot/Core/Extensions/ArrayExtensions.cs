namespace NitroxDiscordBot.Core.Extensions;

public static class ArrayExtensions
{
    public static bool AnyTrue<TSource,TCompare>(this TSource[] array, Func<TSource, TCompare, bool> predicate, TCompare lambdaParameter)
    {
        bool anyMatch = false;
        foreach (TSource item in array)
        {
            if (predicate(item, lambdaParameter))
            {
                anyMatch = true;
                break;
            }
        }
        if (!anyMatch)
        {
            return false;
        }
        return true;
    }
}