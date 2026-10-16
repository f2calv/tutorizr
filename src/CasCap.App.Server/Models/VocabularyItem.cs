namespace CasCap.Models;

/// <summary>A German word or phrase the learner is practising.</summary>
public sealed record VocabularyItem
{
    /// <summary>German word or phrase, with its article for nouns, e.g. <c>der Bahnhof</c>.</summary>
    [Description("German word or phrase, with its article for nouns.")]
    public required string Term { get; init; }

    /// <summary>English meaning.</summary>
    [Description("English meaning.")]
    public required string Meaning { get; init; }

    /// <summary>Level the term belongs to.</summary>
    [Description("CEFR level the term belongs to.")]
    public required CefrLevel Level { get; init; }

    /// <summary>Spaced-repetition box from 1 (new or forgotten) to <see cref="VocabularySchedule.MaxBox"/> (well known).</summary>
    [Description("Spaced-repetition box from 1 (new or forgotten) to 5 (well known).")]
    public required int Box { get; init; }

    /// <summary>When the term was added.</summary>
    [Description("When the term was added, in UTC.")]
    public required DateTimeOffset AddedUtc { get; init; }

    /// <summary>When the term is next due for review.</summary>
    [Description("When the term is next due for review, in UTC.")]
    public required DateTimeOffset DueUtc { get; init; }

    /// <summary>When the term was last reviewed, if ever.</summary>
    [Description("When the term was last reviewed, in UTC; null if never reviewed.")]
    public DateTimeOffset? LastReviewedUtc { get; init; }

    /// <summary>Number of correct answers.</summary>
    [Description("Number of correct answers.")]
    public int CorrectCount { get; init; }

    /// <summary>Number of incorrect answers.</summary>
    [Description("Number of incorrect answers.")]
    public int IncorrectCount { get; init; }
}
