namespace CasCap.Models;

/// <summary>A word, phrase or other item the learner is practising with spaced repetition.</summary>
public sealed record VocabularyItem
{
    /// <summary>The item as taught, including any article or grammatical marker, e.g. <c>der Bahnhof</c> in German.</summary>
    [Description("Word or phrase in the language being learned, including any article or grammatical marker.")]
    public required string Term { get; init; }

    /// <summary>Meaning in the learner's own language.</summary>
    [Description("Meaning in the learner's own language.")]
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
