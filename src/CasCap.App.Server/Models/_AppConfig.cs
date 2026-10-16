namespace CasCap.Models;

/// <summary>Application telemetry and endpoint configuration bound from <c>AppConfig</c>.</summary>
public sealed record AppConfig : IAppConfig, IMetricsConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => nameof(AppConfig);

    /// <inheritdoc/>
    [Required, MinLength(1)]
    public string MetricNamePrefix { get; init; } = "tutorizr";

    /// <inheritdoc/>
    [Required, MinLength(1)]
    public string OtelServiceName { get; init; } = "tutorizr";

    /// <inheritdoc/>
    public Uri? OtlpExporterEndpoint { get; init; }

    /// <summary>URL path of the stateless Streamable HTTP MCP endpoint.</summary>
    /// <remarks>Defaults to <c>/mcp</c>.</remarks>
    [Required, MinLength(1)]
    public string McpUrl { get; init; } = "/mcp";
}
