namespace CasCap.Models;

/// <summary>Summary of the learner's level and practice backlog.</summary>
public sealed record LearnerProfile
{
    /// <summary>Level the learner is working towards.</summary>
    [Description("CEFR level the learner is working towards.")]
    public required CefrLevel TargetLevel { get; init; }

    /// <summary>Current estimated level, if one has been set.</summary>
    [Description("Current estimated CEFR level; null until one has been set.")]
    public CefrLevel? EstimatedLevel { get; init; }

    /// <summary>When the estimated level was last set.</summary>
    [Description("When the estimated level was last set, in UTC; null until one has been set.")]
    public DateTimeOffset? EstimatedLevelUpdatedUtc { get; init; }

    /// <summary>Number of vocabulary items being practised.</summary>
    [Description("Number of vocabulary items being practised.")]
    public required long VocabularyCount { get; init; }

    /// <summary>Number of vocabulary items due for review now.</summary>
    [Description("Number of vocabulary items due for review now.")]
    public required long DueVocabularyCount { get; init; }

    /// <summary>Number of retained mistakes.</summary>
    [Description("Number of retained mistakes.")]
    public required long MistakeCount { get; init; }

    /// <summary>When this summary was read.</summary>
    [Description("When this summary was read, in UTC.")]
    public required DateTimeOffset RetrievedUtc { get; init; }
}
