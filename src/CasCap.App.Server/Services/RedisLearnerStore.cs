using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>Redis-backed <see cref="ILearnerStore"/> using the keys in <see cref="TutorConfig"/>.</summary>
public sealed class RedisLearnerStore(
    IOptions<TutorConfig> tutorConfig,
    IConnectionMultiplexer connectionMultiplexer,
    LearnerMcpContext learnerContext) : ILearnerStore
{
    private const string LevelField = "estimatedLevel";
    private const string LevelUpdatedField = "estimatedLevelUpdatedUtc";

    private IDatabase Db => connectionMultiplexer.GetDatabase();
    private string ProfileKey => learnerContext.GetKey(tutorConfig.Value.ProfileKey);
    private string VocabularyKey => learnerContext.GetKey(tutorConfig.Value.VocabularyKey);
    private string VocabularyDueKey => learnerContext.GetKey(tutorConfig.Value.VocabularyDueKey);
    private string MistakesKey => learnerContext.GetKey(tutorConfig.Value.MistakesKey);

    /// <inheritdoc/>
    public async Task<LearnerLevel> GetLevelAsync(CancellationToken cancellationToken)
    {
        var values = await Db.HashGetAsync(ProfileKey, [LevelField, LevelUpdatedField]);
        CefrLevel? level = values[0].HasValue && Enum.TryParse<CefrLevel>(values[0].ToString(), out var parsed) ? parsed : null;
        DateTimeOffset? updated = values[1].HasValue ? DateTimeOffset.Parse(values[1].ToString(), System.Globalization.CultureInfo.InvariantCulture) : null;
        return new LearnerLevel(level, updated);
    }

    /// <inheritdoc/>
    public Task SetLevelAsync(CefrLevel level, DateTimeOffset updatedUtc, CancellationToken cancellationToken) =>
        Db.HashSetAsync(ProfileKey,
        [
            new HashEntry(LevelField, level.ToString()),
            new HashEntry(LevelUpdatedField, updatedUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture)),
        ]);

    /// <inheritdoc/>
    public async Task<VocabularyItem?> GetVocabularyAsync(string normalisedTerm, CancellationToken cancellationToken)
    {
        var value = await Db.HashGetAsync(VocabularyKey, normalisedTerm);
        return value.HasValue ? JsonSerializer.Deserialize<VocabularyItem>(value.ToString()) : null;
    }

    /// <inheritdoc/>
    public async Task SaveVocabularyAsync(string normalisedTerm, VocabularyItem item, CancellationToken cancellationToken)
    {
        var transaction = Db.CreateTransaction();
        _ = transaction.HashSetAsync(VocabularyKey, normalisedTerm, JsonSerializer.Serialize(item));
        _ = transaction.SortedSetAddAsync(VocabularyDueKey, normalisedTerm, item.DueUtc.ToUnixTimeMilliseconds());
        await transaction.ExecuteAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VocabularyItem>> GetDueVocabularyAsync(DateTimeOffset nowUtc, int maxItems, CancellationToken cancellationToken)
    {
        var terms = await Db.SortedSetRangeByScoreAsync(VocabularyDueKey,
            stop: nowUtc.ToUnixTimeMilliseconds(), take: maxItems);
        if (terms.Length == 0)
            return [];

        var values = await Db.HashGetAsync(VocabularyKey, terms);
        return [.. values.Where(value => value.HasValue).Select(value => JsonSerializer.Deserialize<VocabularyItem>(value.ToString())!)];
    }

    /// <inheritdoc/>
    public async Task<(long Total, long Due)> CountVocabularyAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var total = await Db.HashLengthAsync(VocabularyKey);
        var due = await Db.SortedSetLengthAsync(VocabularyDueKey, max: nowUtc.ToUnixTimeMilliseconds());
        return (total, due);
    }

    /// <inheritdoc/>
    public async Task AddMistakeAsync(MistakeEntry entry, CancellationToken cancellationToken)
    {
        var transaction = Db.CreateTransaction();
        _ = transaction.SortedSetAddAsync(MistakesKey, JsonSerializer.Serialize(entry), entry.RecordedUtc.UtcTicks);
        _ = transaction.SortedSetRemoveRangeByRankAsync(MistakesKey, 0, -(tutorConfig.Value.MaxMistakes + 1));
        await transaction.ExecuteAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MistakeEntry>> GetRecentMistakesAsync(int maxItems, CancellationToken cancellationToken)
    {
        var values = await Db.SortedSetRangeByRankAsync(MistakesKey, 0, maxItems - 1, Order.Descending);
        return [.. values.Select(value => JsonSerializer.Deserialize<MistakeEntry>(value.ToString())!)];
    }

    /// <inheritdoc/>
    public Task<long> CountMistakesAsync(CancellationToken cancellationToken) =>
        Db.SortedSetLengthAsync(MistakesKey);
}
