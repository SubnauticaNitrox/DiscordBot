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
}