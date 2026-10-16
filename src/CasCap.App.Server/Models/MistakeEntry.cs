namespace CasCap.Models;

/// <summary>A mistake the learner made and its correction.</summary>
public sealed record MistakeEntry
{
    /// <summary>When the mistake was recorded.</summary>
    [Description("When the mistake was recorded, in UTC.")]
    public required DateTimeOffset RecordedUtc { get; init; }

    /// <summary>Kind of mistake.</summary>
    [Description("Kind of mistake.")]
    public required MistakeCategory Category { get; init; }

    /// <summary>What the learner wrote or said.</summary>
    [Description("What the learner wrote or said.")]
    public required string Incorrect { get; init; }

    /// <summary>The corrected German.</summary>
    [Description("The corrected German.")]
    public required string Correction { get; init; }

    /// <summary>Short explanation of the rule, if one was given.</summary>
    [Description("Short explanation of the rule; null if none was given.")]
    public string? Explanation { get; init; }
}
