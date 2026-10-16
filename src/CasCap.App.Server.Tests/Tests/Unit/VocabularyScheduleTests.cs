namespace CasCap.Tests.Unit;

public sealed class VocabularyScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [Trait("Category", "Scheduling")]
    [InlineData("der Bahnhof", "der bahnhof")]
    [InlineData("  Der   Bahnhof ", "der bahnhof")]
    [InlineData("Guten\tMorgen", "guten morgen")]
    public void Normalise(string term, string expected) => Assert.Equal(expected, VocabularySchedule.Normalise(term));

    [Fact]
    [Trait("Category", "Scheduling")]
    public void CreateNew()
    {
        var item = VocabularySchedule.CreateNew(" der Bahnhof ", " station ", CefrLevel.A2, Now);

        Assert.Equal("der Bahnhof", item.Term);
        Assert.Equal("station", item.Meaning);
        Assert.Equal(1, item.Box);
        Assert.Equal(Now, item.DueUtc);
        Assert.Null(item.LastReviewedUtc);
    }

    [Theory]
    [Trait("Category", "Scheduling")]
    [InlineData(1, 2, 2)]
    [InlineData(2, 3, 4)]
    [InlineData(4, 5, 16)]
    [InlineData(5, 5, 16)]
    public void Review_CorrectPromotes(int box, int expectedBox, int expectedDays)
    {
        var item = VocabularySchedule.CreateNew("die Fahrkarte", "ticket", CefrLevel.A2, Now) with { Box = box };

        var reviewed = VocabularySchedule.Review(item, correct: true, Now);

        Assert.Equal(expectedBox, reviewed.Box);
        Assert.Equal(Now.AddDays(expectedDays), reviewed.DueUtc);
        Assert.Equal(1, reviewed.CorrectCount);
        Assert.Equal(Now, reviewed.LastReviewedUtc);
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public void Review_IncorrectResets()
    {
        var item = VocabularySchedule.CreateNew("die Fahrkarte", "ticket", CefrLevel.A2, Now) with { Box = 4 };

        var reviewed = VocabularySchedule.Review(item, correct: false, Now);

        Assert.Equal(1, reviewed.Box);
        Assert.Equal(Now + VocabularySchedule.RetryDelay, reviewed.DueUtc);
        Assert.Equal(1, reviewed.IncorrectCount);
    }
}
