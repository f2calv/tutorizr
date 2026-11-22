namespace CasCap.Models;

/// <summary>Learner-progress storage settings bound from <c>CasCap:TutorConfig</c>.</summary>
public sealed record TutorConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(TutorConfig)}";

    /// <summary>Level the learner is working towards.</summary>
    /// <remarks>Defaults to <see cref="CefrLevel.B1"/>.</remarks>
    public CefrLevel TargetLevel { get; init; } = CefrLevel.B1;

    /// <summary>Redis hash pattern holding one login's estimated level; must end with <c>:{learner}</c>.</summary>
    [Required, RegularExpression(@"^[a-z0-9:-]+:\{learner\}$")]
    public string ProfileKey { get; init; } = "tutor:snapshot:profile:{learner}";

    /// <summary>Redis hash holding one vocabulary item per normalised term.</summary>
    [Required, RegularExpression(@"^[a-z0-9:-]+:\{learner\}$")]
    public string VocabularyKey { get; init; } = "tutor:snapshot:vocabulary:{learner}";

    /// <summary>Redis sorted set of normalised terms scored by their next review time in Unix milliseconds.</summary>
    [Required, RegularExpression(@"^[a-z0-9:-]+:\{learner\}$")]
    public string VocabularyDueKey { get; init; } = "tutor:series:vocabulary-due:{learner}";

    /// <summary>Redis sorted set of mistakes scored by their recording time in UTC ticks.</summary>
    [Required, RegularExpression(@"^[a-z0-9:-]+:\{learner\}$")]
    public string MistakesKey { get; init; } = "tutor:series:mistakes:{learner}";

    /// <summary>Number of most recent mistakes retained per login.</summary>
    /// <remarks>Defaults to <c>500</c>.</remarks>
    [Range(1, 100_000)]
    public int MaxMistakes { get; init; } = 500;
}
