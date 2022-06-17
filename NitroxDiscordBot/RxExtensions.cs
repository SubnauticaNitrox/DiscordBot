using System.Reactive.Linq;
using Microsoft.Extensions.Options;

namespace NitroxDiscordBot;

public static class RxExtensions
{
    public static IObservable<T> CreateObservable<T>(this IOptionsMonitor<T> options) where T : class
    {
        return Observable.Create<T>(observer => options.OnChange(observer.OnNext));
    }
}