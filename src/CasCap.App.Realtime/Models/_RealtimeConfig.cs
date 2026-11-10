namespace CasCap.Models;

/// <summary>Azure OpenAI Realtime session configuration bound from <c>RealtimeConfig</c>.</summary>
/// <remarks>Properties have public setters because MAUI's generated configuration binding skips init-only properties.</remarks>
public sealed record RealtimeConfig
{
    /// <summary>Configuration section name.</summary>
    public static string ConfigurationSectionName => nameof(RealtimeConfig);

    /// <summary>Azure OpenAI or Foundry resource endpoint, for example <c>https://example.openai.azure.com</c>.</summary>
    [Required]
    public Uri? Endpoint { get; set; }

    /// <summary>Name of the realtime model deployment.</summary>
    [Required, MinLength(1)]
    public string Deployment { get; set; } = "gpt-realtime";

    /// <summary>Microsoft Entra scope requested for the bearer token.</summary>
    [Required, MinLength(1)]
    public string TokenScope { get; set; } = "https://ai.azure.com/.default";

    /// <summary>Output voice name.</summary>
    [Required, MinLength(1)]
    public string Voice { get; set; } = "marin";

    /// <summary>Session instructions sent to the model.</summary>
    /// <remarks>
    /// Defaults to the German example tutor. TODO: load the tutor definition's instructions from the Agent
    /// Runtime instead of local configuration, so the teaching prompt has one owner; kept local while the
    /// transport is proven.
    /// </remarks>
    [Required, MinLength(1)]
    public string Instructions { get; set; } =
        "You are a patient German tutor in a live voice conversation. Speak mostly German at the learner's level, "
        + "keep each reply short, ask one question at a time and gently correct mistakes.";

    /// <summary>Turn detection mode.</summary>
    public RealtimeTurnDetection TurnDetection { get; set; } = RealtimeTurnDetection.SemanticVad;

    /// <summary>Semantic VAD eagerness: <c>low</c>, <c>medium</c>, <c>high</c> or <c>auto</c>.</summary>
    /// <remarks>Defaults to <c>low</c> so a language learner can pause mid-sentence.</remarks>
    [Required, MinLength(1)]
    public string Eagerness { get; set; } = "low";

    /// <summary>Learner transcription model, or empty to disable learner transcripts.</summary>
    public string? InputTranscriptionModel { get; set; } = "whisper-1";

    /// <summary>Language hint for learner transcription, or empty to auto-detect; defaults to the German example.</summary>
    public string? InputTranscriptionLanguage { get; set; } = "de";
}
