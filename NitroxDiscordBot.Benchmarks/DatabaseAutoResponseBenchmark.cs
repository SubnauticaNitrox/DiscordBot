using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Db;
using NitroxDiscordBot.Db.Models;

namespace NitroxDiscordBot.Benchmarks;

[MemoryDiagnoser]
public class DatabaseAutoResponseBenchmark
{
    readonly BotContext db = new();
    private AutoResponse[]? autoResponses;

    [GlobalSetup]
    public void Setup()
    {
        db.Database.EnsureDeleted();
        db.Database.Migrate();
        db.AutoResponses.Add(new AutoResponse()
        {
            Name = "Benchmark",
            Filters =
            [
                new AutoResponse.Filter()
                {
                    Type = AutoResponse.Filter.Types.AnyChannel,
                    Value = ["125195081295"]
                }
            ],
            Responses =
            [
                new AutoResponse.Response()
                {
                    Type = AutoResponse.Response.Types.MessageUsers,
                    Value = ["845642345122"]
                }
            ]
        });
        db.SaveChanges();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        db.Database.EnsureDeleted();
        db.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int Database()
    {
        int num = 0;
        foreach (AutoResponse response in db.AutoResponses
                     .Include(r => r.Filters)
                     .Include(r => r.Responses))
        {
            num += response.Name.Length;
        }
        return num;
    }

    [Benchmark]
    public int MemCached()
    {
        int num = 0;
        autoResponses ??= db.AutoResponses
            .Include(r => r.Filters)
            .Include(r => r.Responses).ToArray();
        foreach (AutoResponse response in autoResponses)
        {
            num += response.Name.Length;
        }
        return num;
    }
}