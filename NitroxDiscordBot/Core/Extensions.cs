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

    public static uint ParseHexToUint(this string hex)
    {
        uint.TryParse(hex.AsSpan().TrimStart('#'), NumberStyles.HexNumber, null, out var number);
        return number;
    }

    public static void AddHostedSingleton<TService>(this IServiceCollection services) where TService : class, IHostedService =>
        services.AddSingleton<TService>().AddHostedService(provider => provider.GetRequiredService<TService>());

    public static bool IsBusy<T>(this Task<T> task) => task is { IsCompleted: false };
    public static bool IsBusy(this Task task) => task is { IsCompleted: false };
}
