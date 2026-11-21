using CasCap.AgentRuntime.Contracts.V1.Constants;
using System.Net;
using System.Text.Json;

namespace CasCap.Services;

/// <summary>Reads realtime policy using the configured runtime transport and delegated voice token.</summary>
public sealed class VoiceCapabilityClient(HttpClient httpClient, IOptions<TutorClientConfig> config)
{
    internal async Task<VoiceCapabilities?> GetAsync(CancellationToken cancellationToken)
    {
        var agent = Uri.EscapeDataString(config.Value.AgentName!);
        // TODO: Use the shared route suffix when it ships in the pinned Agentizr package.
        var path = AgentRuntimeRoutes.AgentGroup.Replace("{agentName}", agent, StringComparison.Ordinal)
            + "/realtime/capabilities";
        using var response = await httpClient.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        await response.Content.LoadIntoBufferAsync(4096, cancellationToken);
        using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync(body, CallPageJsonContext.Default.VoiceCapabilities, cancellationToken)
            ?? throw new JsonException("Agent Runtime returned an empty capability response.");
    }
}
