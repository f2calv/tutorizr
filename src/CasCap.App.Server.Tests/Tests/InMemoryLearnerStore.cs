namespace CasCap.TestData;

/// <summary>Dictionary-backed <see cref="ILearnerStore"/> that mirrors the Redis ordering rules.</summary>
internal sealed class InMemoryLearnerStore(int maxMistakes = 500) : ILearnerStore
{
    private readonly Dictionary<string, VocabularyItem> _vocabulary = [];
    private readonly List<MistakeEntry> _mistakes = [];
    private LearnerLevel _level = new(null, null);

    public Task<LearnerLevel> GetLevelAsync(CancellationToken cancellationToken) => Task.FromResult(_level);

    public Task SetLevelAsync(CefrLevel level, DateTimeOffset updatedUtc, CancellationToken cancellationToken)
    {
        _level = new LearnerLevel(level, updatedUtc);
        return Task.CompletedTask;
    }

    public Task<VocabularyItem?> GetVocabularyAsync(string normalisedTerm, CancellationToken cancellationToken) =>
        Task.FromResult(_vocabulary.GetValueOrDefault(normalisedTerm));

    public Task SaveVocabularyAsync(string normalisedTerm, VocabularyItem item, CancellationToken cancellationToken)
    {
        _vocabulary[normalisedTerm] = item;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VocabularyItem>> GetDueVocabularyAsync(DateTimeOffset nowUtc, int maxItems, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<VocabularyItem>>(
            [.. _vocabulary.Values.Where(item => item.DueUtc <= nowUtc).OrderBy(item => item.DueUtc).Take(maxItems)]);

    public Task<(long Total, long Due)> CountVocabularyAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
        Task.FromResult<(long, long)>((_vocabulary.Count, _vocabulary.Values.Count(item => item.DueUtc <= nowUtc)));

    public Task AddMistakeAsync(MistakeEntry entry, CancellationToken cancellationToken)
    {
        _mistakes.Add(entry);
        if (_mistakes.Count > maxMistakes)
            _mistakes.RemoveRange(0, _mistakes.Count - maxMistakes);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MistakeEntry>> GetRecentMistakesAsync(int maxItems, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MistakeEntry>>([.. _mistakes.OrderByDescending(entry => entry.RecordedUtc).Take(maxItems)]);

    public Task<long> CountMistakesAsync(CancellationToken cancellationToken) => Task.FromResult<long>(_mistakes.Count);
}
