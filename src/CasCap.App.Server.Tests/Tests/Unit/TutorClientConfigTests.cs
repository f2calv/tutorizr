using System.ComponentModel.DataAnnotations;

namespace CasCap.Tests.Unit;

/// <summary>Verifies device-entered scopes without MSAL sign-in or platform workloads.</summary>
[Trait("Category", "Authentication")]
public sealed class TutorClientConfigTests
{
    [Theory]
    [InlineData("api://example-api/agent.realtime", false)]
    [InlineData("api://example-api/agent.realtime api://example-api/agent.knowledge.search", true)]
    [InlineData(" \tapi://example-api/agent.realtime\napi://example-api/agent.knowledge.search api://example-api/agent.realtime ", true)]
    public void GetApiScopes(string scopes, bool knowledge)
    {
        var config = new TutorClientConfig { ApiScope = scopes };

        string[] expected = knowledge
            ? ["api://example-api/agent.realtime", "api://example-api/agent.knowledge.search"]
            : ["api://example-api/agent.realtime"];
        Assert.Equal(expected, config.GetApiScopes());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n ")]
    public void Validate_RejectsMissingScopes(string? scopes)
    {
        var config = new TutorClientConfig
        {
            TenantId = "00000000-0000-0000-0000-000000000000",
            ClientId = "00000000-0000-0000-0000-000000000000",
            ApiScope = scopes,
            AgentName = "example-tutor",
        };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(config, new(config), results, validateAllProperties: true));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(TutorClientConfig.ApiScope)));
    }
}
