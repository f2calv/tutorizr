namespace CasCap.Constants;

/// <summary>Feature names accepted by <see cref="FeatureConfig.EnabledFeatures"/>.</summary>
public static class FeatureNames
{
    /// <summary>Answers the chat group through the Agent Runtime, with speech-to-text and text-to-speech.</summary>
    public const string Comms = CommsFeatureNames.Comms;

    /// <summary>Serves the tutor tools at <see cref="AppConfig.McpUrl"/> for the Agent Runtime.</summary>
    public const string Mcp = nameof(Mcp);

    /// <summary>Every accepted feature name.</summary>
    public static IReadOnlySet<string> ValidNames { get; } =
        new HashSet<string>([Comms, Mcp], StringComparer.OrdinalIgnoreCase);
}
