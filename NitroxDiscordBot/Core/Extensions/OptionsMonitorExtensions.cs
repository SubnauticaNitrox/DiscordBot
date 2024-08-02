using System.Reactive.Linq;
using Microsoft.Extensions.Options;

namespace NitroxDiscordBot.Core.Extensions;

public static class OptionsMonitorExtensions
{
    public static IObservable<T> AsObservable<T>(this IOptionsMonitor<T> options) where T : class
    {
        return Observable.Create<T>(observer => options.OnChange(observer.OnNext)!);
    }
}