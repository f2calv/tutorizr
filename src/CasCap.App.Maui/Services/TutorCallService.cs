using CasCap.AgentRuntime.Client.Abstractions;

namespace CasCap.Services;

/// <summary>
/// Starts and ends the learner's brokered voice call through the Agent Runtime. The runtime applies the tutor's
/// instructions and runs its tools; this client only exchanges the WebRTC offer and answer.
/// </summary>
/// <param name="client">The Agent Runtime client authenticated as the learner.</param>
/// <param name="config">The validated tutor client settings.</param>
/// <param name="logger">The service logger.</param>
public sealed class TutorCallService(
    IAgentRuntimeClient client,
    IOptions<TutorClientConfig> config,
    ILogger<TutorCallService> logger)
{
    private string? _callId;

    /// <summary>
    /// Starts a call from the web view's SDP offer and returns the SDP answer, or <see langword="null"/> when the
    /// agent offers no realtime calls.
    /// </summary>
    /// <param name="sdpOffer">The WebRTC SDP offer.</param>
    /// <param name="cancellationToken">Cancels call setup.</param>
    public async Task<string?> StartAsync(string sdpOffer, CancellationToken cancellationToken)
    {
        var response = await client.CreateRealtimeCallAsync(config.Value.AgentName!, sdpOffer, cancellationToken);
        _callId = response?.CallId;
        return response?.SdpAnswer;
    }

    /// <summary>Ends the active call, if any, so the runtime hangs up and releases its tool connections.</summary>
    /// <param name="cancellationToken">Cancels the hang-up request.</param>
    public async Task EndAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _callId, null) is not { } callId)
            return;
        try
        {
            await client.EndRealtimeCallAsync(config.Value.AgentName!, callId, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // The runtime still hangs up when the call reaches its maximum duration or loses control.
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning(ex, "{ClassName} could not end the realtime call", nameof(TutorCallService));
        }
    }
}
