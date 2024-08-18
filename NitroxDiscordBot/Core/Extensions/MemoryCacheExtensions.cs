using Cysharp.Text;
using Microsoft.Extensions.Caching.Memory;

namespace NitroxDiscordBot.Core.Extensions;

public static class MemoryCacheExtensions
{
    public static string CreateKey<T1>(this IMemoryCache cache, ReadOnlySpan<char> category, T1 keyPart)
    {
        using Utf16ValueStringBuilder sb = ZString.CreateStringBuilder(true);
        Utf16ValueStringBuilder sbInner = sb;
        ref Utf16ValueStringBuilder refSb = ref sbInner;
        refSb.Append(category);
        refSb.AppendObject(ref keyPart);
        return refSb.ToString();
    }

    public static string CreateKey<T1, T2>(this IMemoryCache cache, ReadOnlySpan<char> category, T1 keyPart, T2 keyPart2)
    {
        using Utf16ValueStringBuilder sb = ZString.CreateStringBuilder(true);
        Utf16ValueStringBuilder sbInner = sb;
        ref Utf16ValueStringBuilder refSb = ref sbInner;
        refSb.Append(category);
        refSb.AppendObject(ref keyPart);
        refSb.AppendObject(ref keyPart2);
        return refSb.ToString();
    }

    private static void AppendObject<T>(this ref Utf16ValueStringBuilder sb, ref T value)
    {
        switch (value)
        {
            case string[] strings:
                sb.AppendJoin("", strings);
                break;
            default:
                sb.Append(value);
                break;
        }
    }
}