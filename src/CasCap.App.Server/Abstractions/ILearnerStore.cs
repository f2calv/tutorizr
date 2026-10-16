namespace CasCap.Abstractions;

/// <summary>Persists the learner's level, vocabulary progress and mistakes.</summary>
public interface ILearnerStore
{
    /// <summary>Reads the learner's estimated level.</summary>
    public Task<LearnerLevel> GetLevelAsync(CancellationToken cancellationToken);

    /// <summary>Stores the learner's estimated level.</summary>
    public Task SetLevelAsync(CefrLevel level, DateTimeOffset updatedUtc, CancellationToken cancellationToken);

    /// <summary>Reads one vocabulary item by its normalised term, or <see langword="null"/> when it is unknown.</summary>
    public Task<VocabularyItem?> GetVocabularyAsync(string normalisedTerm, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces a vocabulary item and its review time.</summary>
    public Task SaveVocabularyAsync(string normalisedTerm, VocabularyItem item, CancellationToken cancellationToken);

    /// <summary>Reads up to <paramref name="maxItems"/> items due at or before <paramref name="nowUtc"/>, oldest first.</summary>
    public Task<IReadOnlyList<VocabularyItem>> GetDueVocabularyAsync(DateTimeOffset nowUtc, int maxItems, CancellationToken cancellationToken);

    /// <summary>Counts all vocabulary items and those due at or before <paramref name="nowUtc"/>.</summary>
    public Task<(long Total, long Due)> CountVocabularyAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken);

    /// <summary>Appends a mistake, discarding the oldest beyond the retention limit.</summary>
    public Task AddMistakeAsync(MistakeEntry entry, CancellationToken cancellationToken);

    /// <summary>Reads up to <paramref name="maxItems"/> mistakes, newest first.</summary>
    public Task<IReadOnlyList<MistakeEntry>> GetRecentMistakesAsync(int maxItems, CancellationToken cancellationToken);

    /// <summary>Counts retained mistakes.</summary>
    public Task<long> CountMistakesAsync(CancellationToken cancellationToken);
}
