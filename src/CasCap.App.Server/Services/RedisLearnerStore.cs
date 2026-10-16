using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>Redis-backed <see cref="ILearnerStore"/> using the keys in <see cref="TutorConfig"/>.</summary>
public sealed class RedisLearnerStore(IConnectionMultiplexer connectionMultiplexer, IOptions<TutorConfig> tutorConfig) : ILearnerStore
{
    private const string LevelField = "estimatedLevel";
    private const string LevelUpdatedField = "estimatedLevelUpdatedUtc";

    private readonly TutorConfig _config = tutorConfig.Value;

    private IDatabase Db => connectionMultiplexer.GetDatabase();

    /// <inheritdoc/>
    public async Task<LearnerLevel> GetLevelAsync(CancellationToken cancellationToken)
    {
        var values = await Db.HashGetAsync(_config.ProfileKey, [LevelField, LevelUpdatedField]);
        CefrLevel? level = values[0].HasValue && Enum.TryParse<CefrLevel>(values[0].ToString(), out var parsed) ? parsed : null;
        DateTimeOffset? updated = values[1].HasValue ? DateTimeOffset.Parse(values[1].ToString(), System.Globalization.CultureInfo.InvariantCulture) : null;
        return new LearnerLevel(level, updated);
    }

    /// <inheritdoc/>
    public Task SetLevelAsync(CefrLevel level, DateTimeOffset updatedUtc, CancellationToken cancellationToken) =>
        Db.HashSetAsync(_config.ProfileKey,
        [
            new HashEntry(LevelField, level.ToString()),
            new HashEntry(LevelUpdatedField, updatedUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture)),
        ]);

    /// <inheritdoc/>
    public async Task<VocabularyItem?> GetVocabularyAsync(string normalisedTerm, CancellationToken cancellationToken)
    {
        var value = await Db.HashGetAsync(_config.VocabularyKey, normalisedTerm);
        return value.HasValue ? JsonSerializer.Deserialize<VocabularyItem>(value.ToString()) : null;
    }

    /// <inheritdoc/>
    public async Task SaveVocabularyAsync(string normalisedTerm, VocabularyItem item, CancellationToken cancellationToken)
    {
        var transaction = Db.CreateTransaction();
        _ = transaction.HashSetAsync(_config.VocabularyKey, normalisedTerm, JsonSerializer.Serialize(item));
        _ = transaction.SortedSetAddAsync(_config.VocabularyDueKey, normalisedTerm, item.DueUtc.ToUnixTimeMilliseconds());
        await transaction.ExecuteAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VocabularyItem>> GetDueVocabularyAsync(DateTimeOffset nowUtc, int maxItems, CancellationToken cancellationToken)
    {
        var terms = await Db.SortedSetRangeByScoreAsync(_config.VocabularyDueKey,
            stop: nowUtc.ToUnixTimeMilliseconds(), take: maxItems);
        if (terms.Length == 0)
            return [];

        var values = await Db.HashGetAsync(_config.VocabularyKey, terms);
        return [.. values.Where(value => value.HasValue).Select(value => JsonSerializer.Deserialize<VocabularyItem>(value.ToString())!)];
    }

    /// <inheritdoc/>
    public async Task<(long Total, long Due)> CountVocabularyAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var total = await Db.HashLengthAsync(_config.VocabularyKey);
        var due = await Db.SortedSetLengthAsync(_config.VocabularyDueKey, max: nowUtc.ToUnixTimeMilliseconds());
        return (total, due);
    }

    /// <inheritdoc/>
    public async Task AddMistakeAsync(MistakeEntry entry, CancellationToken cancellationToken)
    {
        var transaction = Db.CreateTransaction();
        _ = transaction.SortedSetAddAsync(_config.MistakesKey, JsonSerializer.Serialize(entry), entry.RecordedUtc.UtcTicks);
        _ = transaction.SortedSetRemoveRangeByRankAsync(_config.MistakesKey, 0, -(_config.MaxMistakes + 1));
        await transaction.ExecuteAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MistakeEntry>> GetRecentMistakesAsync(int maxItems, CancellationToken cancellationToken)
    {
        var values = await Db.SortedSetRangeByRankAsync(_config.MistakesKey, 0, maxItems - 1, Order.Descending);
        return [.. values.Select(value => JsonSerializer.Deserialize<MistakeEntry>(value.ToString())!)];
    }

    /// <inheritdoc/>
    public Task<long> CountMistakesAsync(CancellationToken cancellationToken) =>
        Db.SortedSetLengthAsync(_config.MistakesKey);
}
