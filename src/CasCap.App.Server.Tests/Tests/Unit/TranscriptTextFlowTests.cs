namespace CasCap.Tests.Unit;

/// <summary>Checks lossless text continuation beside a floating conversation action.</summary>
[Trait("Category", "Layout")]
public sealed class TranscriptTextFlowTests
{
    [Fact]
    public void FindSplit_PrefersWordBoundary()
    {
        // Regression for the full-height copy column reported during issue #26 acceptance.
        const string text = "first second third";
        var split = TranscriptTextFlow.FindSplit(text, prefix => prefix.Length <= 8);
        Assert.Equal("first ", text[..split]);
        Assert.Equal("second third", text[split..]);
    }

    [Theory]
    [InlineData("\U0001F642abc")]
    [InlineData("a\u0308bc")]
    public void FindSplit_KeepsTextElementsWhole(string text)
    {
        var split = TranscriptTextFlow.FindSplit(text, prefix => prefix.Length <= 1);
        Assert.Equal(2, split);
        Assert.Equal(text, text[..split] + text[split..]);
    }

    [Fact]
    public void FindSplit_PreservesParagraphBreaks()
    {
        const string text = "alpha\r\n\r\nbeta gamma";
        var split = TranscriptTextFlow.FindSplit(text, prefix => prefix.Length <= 9);
        Assert.Equal("alpha\r\n\r\n", text[..split]);
        Assert.Equal("beta gamma", text[split..]);
    }

    [Fact]
    public void FindSplit_DoesNotCreateContinuationWhenTextFits()
    {
        const string text = "brief reply";
        Assert.Equal(text.Length, TranscriptTextFlow.FindSplit(text, _ => true));
    }
}
