using CasCap.AgentRuntime.Contracts.V1.Constants;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace CasCap.Tests.Unit;

/// <summary>Checks that learner ownership cannot be omitted, malformed or supplied without authentication.</summary>
[Trait("Category", "Authentication")]
public sealed class LearnerMcpContextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("user-a")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:")]
    public void LearnerId_RejectsMissingOrMalformedHeader(string? value)
    {
        var context = CreateContext(value);
        Assert.Throws<McpException>(() => context.LearnerId);
    }

    [Fact]
    public void LearnerId_RejectsUnauthenticatedAndDuplicateHeaders()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers[McpCallerHeaders.CallerId] = new string('a', 64);
        var context = new LearnerMcpContext(new HttpContextAccessor { HttpContext = http });
        Assert.Throws<McpException>(() => context.LearnerId);

        http.User = new ClaimsPrincipal(new ClaimsIdentity("synthetic"));
        http.Request.Headers[McpCallerHeaders.CallerId] = new[] { new string('a', 64), new string('b', 64) };
        Assert.Throws<McpException>(() => context.LearnerId);
    }

    [Theory]
    [InlineData("tutor:snapshot:profile")]
    [InlineData("tutor:snapshot:{learner}:shared")]
    public void Configuration_RejectsUnqualifiedKeys(string pattern)
    {
        var config = new TutorConfig { ProfileKey = pattern };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(config, new ValidationContext(config), results, true));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(TutorConfig.ProfileKey)));
    }

    private static LearnerMcpContext CreateContext(string? value)
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("synthetic")) };
        if (value is not null)
            http.Request.Headers[McpCallerHeaders.CallerId] = value;
        return new LearnerMcpContext(new HttpContextAccessor { HttpContext = http });
    }
}
