namespace CasCap.Models;

/// <summary>Learner-progress storage settings bound from <c>CasCap:TutorConfig</c>.</summary>
public sealed record TutorConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(TutorConfig)}";

    /// <summary>Level the learner is working towards.</summary>
    /// <remarks>Defaults to <see cref="CefrLevel.B1"/>.</remarks>
    public CefrLevel TargetLevel { get; init; } = CefrLevel.B1;

    /// <summary>Redis hash holding the learner's estimated level.</summary>
    [Required, MinLength(1)]
    public string ProfileKey { get; init; } = "tutor:snapshot:profile";

    /// <summary>Redis hash holding one vocabulary item per normalised term.</summary>
    [Required, MinLength(1)]
    public string VocabularyKey { get; init; } = "tutor:snapshot:vocabulary";

    /// <summary>Redis sorted set of normalised terms scored by their next review time in Unix milliseconds.</summary>
    [Required, MinLength(1)]
    public string VocabularyDueKey { get; init; } = "tutor:series:vocabulary-due";

    /// <summary>Redis sorted set of mistakes scored by their recording time in UTC ticks.</summary>
    [Required, MinLength(1)]
    public string MistakesKey { get; init; } = "tutor:series:mistakes";

    /// <summary>Number of most recent mistakes retained.</summary>
    /// <remarks>Defaults to <c>500</c>.</remarks>
    [Range(1, 100_000)]
    public int MaxMistakes { get; init; } = 500;
}
