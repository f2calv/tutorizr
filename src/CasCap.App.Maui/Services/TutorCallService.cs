using CasCap.AgentRuntime.Client.Abstractions;
using CasCap.AgentRuntime.Client.Models;
using CasCap.AgentRuntime.Contracts.V1;
using Microsoft.Maui.Storage;
using System.Security.Cryptography;
using System.Text;

namespace CasCap.Services;

/// <summary>
/// Starts and ends the learner's brokered voice call through the Agent Runtime. The runtime applies the tutor's
/// instructions and runs its tools; this client only exchanges the WebRTC offer and answer.
/// </summary>
/// <param name="clientFactory">
/// Creates the Agent Runtime client authenticated as the learner; deferred because creating it validates settings that a
/// first run on a device has not entered yet.
/// </param>
/// <param name="config">The validated tutor client settings.</param>
/// <param name="logger">The service logger.</param>
public sealed class TutorCallService(
    Func<IAgentRuntimeClient> clientFactory,
    IOptions<TutorClientConfig> config,
    IOptions<AgentRuntimeClientOptions> runtimeConfig,
    Func<MsalAccessTokenProvider> tokenProviderFactory,
    ILogger<TutorCallService> logger)
{
    private static readonly TimeSpan EndTimeout = TimeSpan.FromSeconds(10);
    private string? _callId;
    private Guid? _conversationId;
    private string? _conversationKey;

    /// <summary>Gets whether this configuration has a previously retained voice conversation.</summary>
    public bool CanResume => _conversationId is not null;

    /// <summary>Loads only the last opaque reference; voice transcripts are never persisted on the device.</summary>
    public void RestoreConversation()
    {
        var profile = $"{runtimeConfig.Value.BaseAddress}|{config.Value.TenantId}|{config.Value.ClientId}|{config.Value.AgentName}";
        _conversationKey = "voice-conversation-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profile)));
        _conversationId = Guid.TryParse(Preferences.Default.Get(_conversationKey, string.Empty), out var id) ? id : null;
    }

    /// <summary>Completes interactive sign-in before media starts, since the system browser backgrounds the app.</summary>
    /// <param name="cancellationToken">Cancels sign-in.</param>
    public async Task PrepareAsync(CancellationToken cancellationToken) =>
        _ = await tokenProviderFactory().GetAccessTokenAsync(cancellationToken);

    /// <summary>
    /// Starts a call from the web view's SDP offer and returns the SDP answer, or <see langword="null"/> when the
    /// agent offers no realtime calls.
    /// </summary>
    /// <param name="sdpOffer">The WebRTC SDP offer.</param>
    /// <param name="cancellationToken">Cancels call setup.</param>
    /// <param name="resume">Resumes the last retained voice conversation instead of starting new context.</param>
    /// <param name="retainConversation">Opts into twenty-four-hour server-side text retention.</param>
    public async Task<string?> StartAsync(
        string sdpOffer, CancellationToken cancellationToken, bool resume, bool retainConversation)
    {
        RestoreConversation();
        if (resume && _conversationId is null)
            throw new HttpRequestException("There is no retained conversation to resume.", null, System.Net.HttpStatusCode.Conflict);
        var response = await clientFactory().CreateRealtimeCallAsync(
            config.Value.AgentName!, sdpOffer, cancellationToken, resume ? _conversationId : null,
            retainConversation || resume);
        _callId = response?.CallId;
        if (response is not null)
        {
            _conversationId = response.ConversationId;
            if (_conversationId is { } id)
                Preferences.Default.Set(_conversationKey!, id.ToString("D"));
            else
                Preferences.Default.Remove(_conversationKey!);
        }
        return response?.SdpAnswer;
    }

    /// <summary>
    /// Ends the active call, if any, so the runtime hangs up and releases its tool connections, and returns why it
    /// ended and what it used; <see langword="null"/> when there was no call or the runtime no longer knows it.
    /// </summary>
    /// <param name="cancellationToken">Cancels the hang-up request.</param>
    public async Task<RealtimeCallSummaryResponse?> EndAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _callId, null) is not { } callId)
            return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(EndTimeout);
        try
        {
            return await clientFactory().EndRealtimeCallAsync(config.Value.AgentName!, callId, timeout.Token);
        }
        catch (HttpRequestException ex)
        {
            // The runtime still hangs up when the call reaches its maximum duration or loses control.
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning(ex, "{ClassName} could not end the realtime call", nameof(TutorCallService));
            return null;
        }
        catch (OperationCanceledException ex) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning(ex, "{ClassName} timed out ending the realtime call", nameof(TutorCallService));
            return null;
        }
    }
}
