namespace CasCap.Tests.Unit;

public sealed class TutorMcpQueryServiceTests
{
    private readonly InMemoryLearnerStore _store = new(maxMistakes: 3);
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero));
    private readonly TutorMcpQueryService _service;

    public TutorMcpQueryServiceTests() =>
        _service = new TutorMcpQueryService(_store, Options.Create(new TutorConfig { MaxMistakes = 3 }), _time);

    [Fact]
    [Trait("Category", "Tools")]
    public async Task GetLearnerProfile_Empty()
    {
        var profile = await _service.GetLearnerProfile(TestContext.Current.CancellationToken);

        Assert.Equal(CefrLevel.B1, profile.TargetLevel);
        Assert.Null(profile.EstimatedLevel);
        Assert.Equal(0, profile.VocabularyCount);
        Assert.Equal(0, profile.MistakeCount);
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task SetLearnerLevel()
    {
        var profile = await _service.SetLearnerLevel(CefrLevel.A2, TestContext.Current.CancellationToken);

        Assert.Equal(CefrLevel.A2, profile.EstimatedLevel);
        Assert.Equal(_time.GetUtcNow(), profile.EstimatedLevelUpdatedUtc);
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task VocabularyLifecycle_AddReviewDue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _service.AddVocabulary("der Bahnhof", "station", CefrLevel.A2, cancellationToken);

        Assert.Single(await _service.GetDueVocabulary(cancellationToken: cancellationToken));

        var reviewed = await _service.RecordVocabularyReview("Der  Bahnhof", correct: true, cancellationToken);
        Assert.Equal(2, reviewed.Box);
        Assert.Empty(await _service.GetDueVocabulary(cancellationToken: cancellationToken));

        _time.Advance(TimeSpan.FromDays(2));
        Assert.Single(await _service.GetDueVocabulary(cancellationToken: cancellationToken));
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task AddVocabulary_ExistingKeepsProgress()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _service.AddVocabulary("der Bahnhof", "station", CefrLevel.A2, cancellationToken);
        await _service.RecordVocabularyReview("der Bahnhof", correct: true, cancellationToken);

        var updated = await _service.AddVocabulary("der Bahnhof", "railway station", CefrLevel.A1, cancellationToken);

        Assert.Equal(2, updated.Box);
        Assert.Equal("railway station", updated.Meaning);
        Assert.Equal(CefrLevel.A1, updated.Level);
    }

    [Fact]
    [Trait("Category", "Tools")]
    public async Task RecordVocabularyReview_UnknownTermFails() =>
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() =>
            _service.RecordVocabularyReview("das Unbekannte", correct: true, TestContext.Current.CancellationToken));

    [Fact]
    [Trait("Category", "Tools")]
    public async Task Mistakes_RetainNewestAndFilter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        foreach (var category in new[] { MistakeCategory.Grammar, MistakeCategory.Spelling, MistakeCategory.Grammar, MistakeCategory.WordOrder })
        {
            await _service.RecordMistake(category, "ich habe gegangen", "ich bin gegangen", cancellationToken: cancellationToken);
            _time.Advance(TimeSpan.FromMinutes(1));
        }

        var recent = await _service.GetRecentMistakes(cancellationToken: cancellationToken);
        var grammar = await _service.GetRecentMistakes(category: MistakeCategory.Grammar, cancellationToken: cancellationToken);

        Assert.Equal([MistakeCategory.WordOrder, MistakeCategory.Grammar, MistakeCategory.Spelling], recent.Select(entry => entry.Category));
        Assert.Single(grammar);
    }

    [Theory]
    [Trait("Category", "Tools")]
    [InlineData(0)]
    [InlineData(51)]
    public async Task GetDueVocabulary_RejectsOutOfRangeCount(int maxItems) =>
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() =>
            _service.GetDueVocabulary(maxItems, TestContext.Current.CancellationToken));

    [Theory]
    [Trait("Category", "Tools")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddVocabulary_RejectsBlankTerm(string term) =>
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() =>
            _service.AddVocabulary(term, "meaning", CefrLevel.A1, TestContext.Current.CancellationToken));
}
