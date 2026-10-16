namespace CasCap.Models;

/// <summary>Comma-separated feature flags bound from <c>CasCap:FeatureConfig</c>.</summary>
public sealed record FeatureConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(FeatureConfig)}";

    /// <summary>Comma-separated list of enabled feature names from <see cref="FeatureNames.ValidNames"/>.</summary>
    [Required, MinLength(1)]
    public required string EnabledFeatures { get; init; }

    /// <summary>Parses <see cref="EnabledFeatures"/> into a case-insensitive set.</summary>
    /// <exception cref="InvalidOperationException">No feature, or an unknown feature, is configured.</exception>
    public HashSet<string> GetEnabledFeatures()
    {
        var features = EnabledFeatures
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (features.Count == 0)
            throw new InvalidOperationException(
                $"No features are enabled. Valid names: {string.Join(", ", FeatureNames.ValidNames)}.");

        var unknown = features.Where(feature => !FeatureNames.ValidNames.Contains(feature)).ToList();
        return unknown.Count > 0
            ? throw new InvalidOperationException(
                $"Unrecognised feature name(s): {string.Join(", ", unknown)}. Valid names: {string.Join(", ", FeatureNames.ValidNames)}.")
            : features;
    }
}
