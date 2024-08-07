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
        Assert.IsFalse("".AsSpan().ContainsSentenceWithWordOrderOfAny([]));
        Assert.IsFalse(text.ContainsSentenceWithWordOrderOfAny([]));
        Assert.IsFalse(text.ContainsSentenceWithWordOrderOfAny(["dolor test elit"]));
        Assert.IsFalse(text.ContainsSentenceWithWordOrderOfAny(["ipsum Lorem"]));
        Assert.IsFalse("One, two, three.".AsSpan().ContainsSentenceWithWordOrderOfAny(["one three two"]));
    }
}