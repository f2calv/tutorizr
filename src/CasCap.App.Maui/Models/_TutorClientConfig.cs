namespace CasCap.Models;

/// <summary>Tutor client sign-in and agent settings bound from <c>TutorClientConfig</c>.</summary>
/// <remarks>Properties have public setters because MAUI's generated configuration binding skips init-only properties.</remarks>
public sealed record TutorClientConfig
{
    /// <summary>Configuration section name.</summary>
    public static string ConfigurationSectionName => nameof(TutorClientConfig);

    /// <summary>Microsoft Entra directory (tenant) ID of the tutor's public client registration.</summary>
    [Required, MinLength(1)]
    public string? TenantId { get; set; }

    /// <summary>Application (client) ID of the tutor's public client registration.</summary>
    [Required, MinLength(1)]
    public string? ClientId { get; set; }

    /// <summary>Delegated Agent Runtime scope, for example <c>api://00000000-0000-0000-0000-000000000000/agent.realtime</c>.</summary>
    [Required, MinLength(1)]
    public string? ApiScope { get; set; }

    /// <summary>Name of the Agent Runtime agent whose definition offers realtime calls.</summary>
    [Required, MinLength(1)]
    public string? AgentName { get; set; }
}
