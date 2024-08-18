using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace NitroxDiscordBot.Tests;

[TestClass]
public class MemoryCacheExtensionsTest
{
    private IMemoryCache cache;

    [TestInitialize]
    public void Initialize()
    {
        cache = new MemoryCache(new MemoryCacheOptions());
    }

    [TestMethod]
    public void TestCreateKey()
    {
        string key = cache.CreateKey("filters", 10, new[] { "foo", "bar" });
        key.Should().Be("filters10foobar");
        key = cache.CreateKey("filters", new[] { "foo", "bar" }, new[] { "bar", "foo" });
        key.Should().Be("filtersfoobarbarfoo");
        key = cache.CreateKey("int", 42);
        key.Should().Be("int42");
    }
}