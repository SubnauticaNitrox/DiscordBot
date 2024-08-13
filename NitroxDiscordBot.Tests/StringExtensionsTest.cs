using FluentAssertions;

namespace NitroxDiscordBot.Tests;

[TestClass]
public class StringExtensionsTest
{
    [TestMethod]
    public void TestContainsWordsInOrder()
    {
        Assert.IsTrue("".AsSpan().ContainsWordsInOrder(""));
        Assert.IsTrue("A".AsSpan().ContainsWordsInOrder(""));
        Assert.IsTrue("A".AsSpan().ContainsWordsInOrder("A"));
        Assert.IsTrue("A B".AsSpan().ContainsWordsInOrder("A B"));
        Assert.IsTrue("    A    B   ".AsSpan().ContainsWordsInOrder("A B"));
        Assert.IsTrue("hello world".AsSpan().ContainsWordsInOrder("hello world"));
        Assert.IsTrue("hello good world".AsSpan().ContainsWordsInOrder("hello world"));
        Assert.IsTrue("Can you tell me the way to the nearest hospital?".AsSpan().ContainsWordsInOrder("tell nearest hospital"));
        Assert.IsTrue("Can you tell me the way to the nearest hospital?".AsSpan().ContainsWordsInOrder("tell nearest hospital"));
        Assert.IsFalse("A".AsSpan().ContainsWordsInOrder("B"));
        Assert.IsFalse("A B".AsSpan().ContainsWordsInOrder("B A"));
        Assert.IsFalse("world hello".AsSpan().ContainsWordsInOrder("hello world"));
        Assert.IsFalse("Can you tell me the way to the nearest hospital?".AsSpan().ContainsWordsInOrder("tell hospital nearest"));
        Assert.IsFalse("Can you tell me the way to the nearest hospital!?".AsSpan().ContainsWordsInOrder("tell hospital nearest"));
    }

    [TestMethod]
    public void TestContainsSentenceWithWordOrderOfAny()
    {
        ReadOnlySpan<char> text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Donec nec libero at nibh tristique viverra non non felis.".AsSpan();

        Assert.IsTrue(text.ContainsSentenceWithWordOrderOfAny(["lorem ipsum"]));
        Assert.IsTrue(text.ContainsSentenceWithWordOrderOfAny(["sobarma", "lorem ipsum"]));
        Assert.IsTrue(text.ContainsSentenceWithWordOrderOfAny(["sobarma", "nibh"]));
        Assert.IsTrue(text.ContainsSentenceWithWordOrderOfAny(["sobarma", "lorem ipsum dolor"]));
        Assert.IsTrue(text.ContainsSentenceWithWordOrderOfAny(["lorem ipsum", "ipsum lorem"]));
        Assert.IsTrue("Lorem ipsum : dolor sit amet".AsSpan().ContainsSentenceWithWordOrderOfAny(["ipsum lorem", "lorem ipsum"]));
        Assert.IsTrue("A.B.C".AsSpan().ContainsSentenceWithWordOrderOfAny(["c"]));
        Assert.IsTrue("One, two, three.".AsSpan().ContainsSentenceWithWordOrderOfAny(["one two three"]));
        Assert.IsTrue("One, two, three?".AsSpan().ContainsSentenceWithWordOrderOfAny(["one two three"]));
        Assert.IsTrue("One, two, three?!?!?".AsSpan().ContainsSentenceWithWordOrderOfAny(["one two three"]));
        Assert.IsTrue("!!One, two, three?!?!?".AsSpan().ContainsSentenceWithWordOrderOfAny(["one two three"]));
        Assert.IsTrue("hi i have a friend witch can do help?".AsSpan().ContainsSentenceWithWordOrderOfAny(["I can help"]));
        Assert.IsFalse("".AsSpan().ContainsSentenceWithWordOrderOfAny([]));
        Assert.IsFalse(text.ContainsSentenceWithWordOrderOfAny([]));
        Assert.IsFalse(text.ContainsSentenceWithWordOrderOfAny(["dolor test elit"]));
        Assert.IsFalse(text.ContainsSentenceWithWordOrderOfAny(["ipsum Lorem"]));
        Assert.IsFalse("One, two, three.".AsSpan().ContainsSentenceWithWordOrderOfAny(["one three two"]));
    }

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
}