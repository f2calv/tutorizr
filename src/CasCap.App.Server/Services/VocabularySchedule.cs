namespace CasCap.Services;

/// <summary>Leitner spaced-repetition schedule for vocabulary reviews.</summary>
public static class VocabularySchedule
{
    /// <summary>Highest box; a term stays there once it is well known.</summary>
    public const int MaxBox = 5;

    private static readonly TimeSpan[] Intervals =
    [
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(2),
        TimeSpan.FromDays(4),
        TimeSpan.FromDays(8),
        TimeSpan.FromDays(16),
    ];

    /// <summary>Delay before a forgotten term is asked again.</summary>
    public static TimeSpan RetryDelay { get; } = TimeSpan.FromMinutes(10);

    /// <summary>Normalises a term into its storage key: trimmed, single-spaced and lower case.</summary>
    public static string Normalise(string term) =>
        string.Join(' ', term.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    /// <summary>Creates a new item in box 1, due immediately.</summary>
    public static VocabularyItem CreateNew(string term, string meaning, CefrLevel level, DateTimeOffset nowUtc) => new()
    {
        Term = term.Trim(),
        Meaning = meaning.Trim(),
        Level = level,
        Box = 1,
        AddedUtc = nowUtc,
        DueUtc = nowUtc,
    };

    /// <summary>
    /// Applies a review: a correct answer promotes the term one box and waits that box's interval; an
    /// incorrect answer returns it to box 1 and asks again after <see cref="RetryDelay"/>.
    /// </summary>
    public static VocabularyItem Review(VocabularyItem item, bool correct, DateTimeOffset nowUtc)
    {
        if (!correct)
        {
            return item with
            {
                Box = 1,
                DueUtc = nowUtc + RetryDelay,
                LastReviewedUtc = nowUtc,
                IncorrectCount = item.IncorrectCount + 1,
            };
        }

        var box = Math.Min(item.Box + 1, MaxBox);
        return item with
        {
            Box = box,
            DueUtc = nowUtc + Intervals[box - 1],
            LastReviewedUtc = nowUtc,
            CorrectCount = item.CorrectCount + 1,
        };
    }
}
