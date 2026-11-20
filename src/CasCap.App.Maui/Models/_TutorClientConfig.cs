using System.ComponentModel.DataAnnotations;

namespace CasCap.Models;

/// <summary>Tutor client sign-in and agent settings bound from <c>TutorClientConfig</c>.</summary>
/// <remarks>Properties have public setters because MAUI's generated configuration binding skips init-only properties.</remarks>
public sealed record TutorClientConfig : IValidatableObject
{
    /// <summary>Configuration section name.</summary>
    public static string ConfigurationSectionName => nameof(TutorClientConfig);

    /// <summary>Microsoft Entra directory (tenant) ID of the tutor's public client registration.</summary>
    [Required, MinLength(1)]
    public string? TenantId { get; set; }

    /// <summary>Application (client) ID of the tutor's public client registration.</summary>
    [Required, MinLength(1)]
    public string? ClientId { get; set; }

    /// <summary>Whitespace-separated delegated Agent Runtime scopes; a single realtime scope remains valid.</summary>
    [Required, MinLength(1)]
    public string? ApiScope { get; set; }

    /// <summary>Name of the Agent Runtime agent whose definition offers realtime calls.</summary>
    [Required, MinLength(1)]
    public string? AgentName { get; set; }

    /// <summary>Returns distinct scope entries for one MSAL access-token request.</summary>
    public string[] GetApiScopes() =>
        (ApiScope ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal).ToArray();

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (GetApiScopes().Length == 0)
        {
            string[] members = [nameof(ApiScope)];
            yield return new("Enter at least one runtime API scope.", members);
        }
    }
}
