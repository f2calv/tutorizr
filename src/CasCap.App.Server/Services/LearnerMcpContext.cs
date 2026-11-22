using CasCap.AgentRuntime.Contracts.V1.Constants;
using ModelContextProtocol;

namespace CasCap.Services;

/// <summary>Resolves learner ownership from an authenticated runtime's request, never model arguments.</summary>
public sealed class LearnerMcpContext(IHttpContextAccessor httpContextAccessor)
{
    /// <summary>Gets the opaque app-scoped login identity, or fails before accessing learner storage.</summary>
    public string LearnerId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context?.User.Identity?.IsAuthenticated is not true)
                throw new McpException("Learner tools require an authenticated runtime request.");

            var values = context.Request.Headers[McpCallerHeaders.CallerId];
            if (values.Count != 1 || values[0] is not { Length: 64 } id
                || id.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
                throw new McpException("Learner tools require a valid runtime-asserted login identity.");
            return id;
        }
    }

    /// <summary>Qualifies a configured key with the current learner's identity.</summary>
    public string GetKey(string pattern) => pattern.Replace("{learner}", LearnerId, StringComparison.Ordinal);
}
