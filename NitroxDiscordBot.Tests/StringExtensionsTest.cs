using System.Text.RegularExpressions;
using FluentAssertions;

namespace NitroxDiscordBot.Tests;

[TestClass]
public class StringExtensionsTest
{
    [TestMethod]
    public void TestOfParsable()
    {
        string[] data = ["test", 106248L.ToString(), 102510124L.ToString(), "invalid", 7236202510124L.ToString(), "oops"];
        long[] expected = [106248L, 102510124L, 7236202510124L];

        data.OfParsable<ulong>().Should().BeEquivalentTo(expected);
        Span<ulong> ulongStack = stackalloc ulong[data.Length];
        data.OfParsable(ref ulongStack);
        ulongStack.ToArray().Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public void TestContainsParsable()
    {
        string[] data = ["test", 106248L.ToString(), 102510124L.ToString(), "invalid", 7236202510124L.ToString(), "oops"];

        data.ContainsParsable<ulong>(106248L).Should().BeTrue();
        data.ContainsParsable<ulong>(7236202510124L).Should().BeTrue();
        data.ContainsParsable<ulong>(0L).Should().BeFalse();
        data.ContainsParsable<ulong>(7236202510125L).Should().BeFalse();
    }

    [TestMethod]
    public void TestCreateRegexesForAnyWordGroupInOrderInSentence()
    {
        Regex[] regexes = new[] { "play game|subnautica me" }.CreateRegexesForAnyWordGroupInOrderInSentence();
        regexes.Should().HaveCount(1);
        regexes[0].IsMatch("Does anyone wanne play subnautica with me?").Should().BeTrue();
        regexes[0].IsMatch("Thanks everyone. Btw, does anyone wanne play subnautica with me?").Should().BeTrue();
        regexes[0].IsMatch("play game me").Should().BeTrue();
        regexes[0].IsMatch("Does anyone wanne play subnautica?").Should().BeFalse();
        regexes[0].IsMatch("game play").Should().BeFalse();
        regexes[0].IsMatch("game play me").Should().BeFalse();
    }

    [TestMethod]
    public void TestLimit()
    {
        "".Limit(100).Should().Be("");
        "".Limit(0).Should().Be("");
        "".Limit(-1).Should().Be("");
        "Hello, world!".Limit(4).Should().Be("Hell");
        "Hello".Limit(4, "...").Should().Be("H...");
        "Hell".Limit(4, "..").Should().Be("He..");
        "Hell".Limit(4, "...").Should().Be("H...");
        "Hell".Limit(4, "....").Should().Be("....");
        "Hell".Limit(4, ".....").Should().Be("....");
        "Hel".Limit(4, "...").Should().Be("H...");
        "H".Limit(4, "...").Should().Be("H");
        "".Limit(4, "...").Should().Be("");
        "".Limit(4).Should().Be("");
        "".Limit(1).Should().Be("");
        "".Limit(1, "..").Should().Be(".");
        "".Limit(2, "_._").Should().Be("_.");
    }
}