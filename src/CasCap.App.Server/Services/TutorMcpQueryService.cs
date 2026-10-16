namespace CasCap.Services;

/// <summary>Exposes the learner's level, vocabulary practice and mistake log as MCP tools.</summary>
/// <remarks>
/// The MCP server activates this type per call, so every constructor parameter must be resolvable
/// from DI. Learner content is never logged.
/// </remarks>
[McpServerToolType]
public sealed class TutorMcpQueryService(ILearnerStore store, IOptions<TutorConfig> tutorConfig, TimeProvider timeProvider)
{
    private const int MaxListItems = 50;
    private const int MaxTextLength = 500;

    /// <summary>Returns the learner's target and estimated level and practice backlog.</summary>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets the learner's target level, current estimated level and how much vocabulary and how many mistakes are on record. Call this at the start of a lesson to pitch the difficulty.")]
    public async Task<LearnerProfile> GetLearnerProfile(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var level = await store.GetLevelAsync(cancellationToken);
        var (total, due) = await store.CountVocabularyAsync(now, cancellationToken);
        return new LearnerProfile
        {
            TargetLevel = tutorConfig.Value.TargetLevel,
            EstimatedLevel = level.EstimatedLevel,
            EstimatedLevelUpdatedUtc = level.UpdatedUtc,
            VocabularyCount = total,
            DueVocabularyCount = due,
            MistakeCount = await store.CountMistakesAsync(cancellationToken),
            RetrievedUtc = now,
        };
    }

    /// <summary>Stores the learner's current estimated level.</summary>
    [McpServerTool(ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Records the learner's current estimated level after a placement exercise or when their performance clearly changes. Returns the updated profile.")]
    public async Task<LearnerProfile> SetLearnerLevel(
        [Description("Estimated CEFR level: A1, A2, B1, B2, C1 or C2.")] CefrLevel level,
        CancellationToken cancellationToken = default)
    {
        await store.SetLevelAsync(level, timeProvider.GetUtcNow(), cancellationToken);
        return await GetLearnerProfile(cancellationToken);
    }

    /// <summary>Adds a term to the learner's vocabulary practice, or updates its meaning and level.</summary>
    [McpServerTool(ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Adds a German word or phrase to the learner's spaced-repetition practice. Adding a term that already exists updates its meaning and level but keeps its review progress.")]
    public async Task<VocabularyItem> AddVocabulary(
        [Description("German word or phrase, with its article for nouns, e.g. 'der Bahnhof'. At most 500 characters.")] string term,
        [Description("English meaning. At most 500 characters.")] string meaning,
        [Description("CEFR level of the term: A1, A2, B1, B2, C1 or C2.")] CefrLevel level,
        CancellationToken cancellationToken = default)
    {
        var key = VocabularySchedule.Normalise(RequireText(term, nameof(term)));
        RequireText(meaning, nameof(meaning));

        var existing = await store.GetVocabularyAsync(key, cancellationToken);
        var item = existing is null
            ? VocabularySchedule.CreateNew(term, meaning, level, timeProvider.GetUtcNow())
            : existing with { Meaning = meaning.Trim(), Level = level };
        await store.SaveVocabularyAsync(key, item, cancellationToken);
        return item;
    }

    /// <summary>Lists vocabulary due for review.</summary>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets the vocabulary due for review now, longest overdue first. Quiz the learner on these terms, then call record_vocabulary_review for each answer.")]
    public async Task<IReadOnlyList<VocabularyItem>> GetDueVocabulary(
        [Description("Maximum number of terms to return, from 1 to 50.")] int maxItems = 10,
        CancellationToken cancellationToken = default) =>
        await store.GetDueVocabularyAsync(timeProvider.GetUtcNow(), RequireCount(maxItems), cancellationToken);

    /// <summary>Records whether the learner answered a vocabulary question correctly.</summary>
    [McpServerTool(ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Records the learner's answer for one practised term and schedules its next review. A correct answer moves it to a longer interval; an incorrect one asks it again in 10 minutes. Call once per answer.")]
    public async Task<VocabularyItem> RecordVocabularyReview(
        [Description("The term exactly as returned by get_due_vocabulary or add_vocabulary.")] string term,
        [Description("True when the learner answered correctly.")] bool correct,
        CancellationToken cancellationToken = default)
    {
        var key = VocabularySchedule.Normalise(RequireText(term, nameof(term)));
        var existing = await store.GetVocabularyAsync(key, cancellationToken)
            ?? throw new ModelContextProtocol.McpException("The term is not in the learner's vocabulary; add it with add_vocabulary first.");

        var item = VocabularySchedule.Review(existing, correct, timeProvider.GetUtcNow());
        await store.SaveVocabularyAsync(key, item, cancellationToken);
        return item;
    }

    /// <summary>Records a mistake and its correction.</summary>
    [McpServerTool(ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Records a mistake the learner made, with the correction, so later lessons can revisit recurring weaknesses. Call once per distinct mistake.")]
    public async Task<MistakeEntry> RecordMistake(
        [Description("Kind of mistake: Grammar, Vocabulary, Spelling, WordOrder, Pronunciation or Other.")] MistakeCategory category,
        [Description("What the learner wrote or said. At most 500 characters.")] string incorrect,
        [Description("The corrected German. At most 500 characters.")] string correction,
        [Description("Optional short explanation of the rule. At most 500 characters.")] string? explanation = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new MistakeEntry
        {
            RecordedUtc = timeProvider.GetUtcNow(),
            Category = category,
            Incorrect = RequireText(incorrect, nameof(incorrect)).Trim(),
            Correction = RequireText(correction, nameof(correction)).Trim(),
            Explanation = string.IsNullOrWhiteSpace(explanation) ? null : RequireText(explanation, nameof(explanation)).Trim(),
        };
        await store.AddMistakeAsync(entry, cancellationToken);
        return entry;
    }

    /// <summary>Lists the learner's most recent mistakes.</summary>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets the learner's most recent mistakes, newest first, optionally of one kind. Use them to choose what to practise.")]
    public async Task<IReadOnlyList<MistakeEntry>> GetRecentMistakes(
        [Description("Maximum number of mistakes to return, from 1 to 50.")] int maxItems = 10,
        [Description("Optional kind of mistake to return: Grammar, Vocabulary, Spelling, WordOrder, Pronunciation or Other. Omit for all kinds.")] MistakeCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var count = RequireCount(maxItems);
        if (category is null)
            return await store.GetRecentMistakesAsync(count, cancellationToken);

        //Filtering happens after the read, so scan the retained window rather than only the newest few.
        var mistakes = await store.GetRecentMistakesAsync(tutorConfig.Value.MaxMistakes, cancellationToken);
        return [.. mistakes.Where(mistake => mistake.Category == category).Take(count)];
    }

    private static string RequireText(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ModelContextProtocol.McpException($"'{name}' is required.")
            : value.Length > MaxTextLength
                ? throw new ModelContextProtocol.McpException($"'{name}' must be at most {MaxTextLength} characters.")
                : value;

    private static int RequireCount(int maxItems) =>
        maxItems is < 1 or > MaxListItems
            ? throw new ModelContextProtocol.McpException($"'maxItems' must be between 1 and {MaxListItems}.")
            : maxItems;
}
