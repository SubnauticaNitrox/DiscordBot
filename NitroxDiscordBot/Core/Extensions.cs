﻿using System.Globalization;
using System.Reactive.Linq;
using Microsoft.Extensions.Options;

namespace NitroxDiscordBot.Core;

public static class Extensions
{
    public static IObservable<T> AsObservable<T>(this IOptionsMonitor<T> options) where T : class
    {
        return Observable.Create<T>(observer => options.OnChange(observer.OnNext)!);
    }

    public static uint HexToUint(this string hex)
    {
        uint.TryParse(hex.AsSpan().TrimStart('#'), NumberStyles.HexNumber, null, out var number);
        return number;
    }
}
