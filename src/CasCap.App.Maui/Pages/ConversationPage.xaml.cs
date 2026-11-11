using CasCap.AgentRuntime.Contracts.V1;
using Microsoft.Identity.Client;
using System.Net;
using System.Text.Json;

namespace CasCap.Pages;

/// <summary>Starts, mutes and ends a live tutor call and shows its state and transient transcript.</summary>
/// <remarks>
/// The hosted web view owns the microphone and WebRTC media; this page exchanges the SDP offer and answer with the
/// Agent Runtime, which applies the tutor's instructions and runs its tools.
/// </remarks>
public sealed partial class ConversationPage : ContentPage
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MediaTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan HangUpTimeout = TimeSpan.FromSeconds(5);
    private const int MaxTranscriptLines = 40;
    private const string MediaFailureMessage =
        "Could not open the microphone or start call audio. Check the app's microphone permission.";

    private readonly TutorCallService _callService;
    private readonly ILogger<ConversationPage> _logger;
    private readonly Queue<string> _transcript = new();
    private TutorCallState _state = TutorCallState.Disconnected;
    private bool _muted;

    /// <summary>Initializes a new instance of the <see cref="ConversationPage"/> class.</summary>
    /// <param name="callService">Starts and ends calls through the Agent Runtime.</param>
    /// <param name="logger">The page logger.</param>
    public ConversationPage(TutorCallService callService, ILogger<ConversationPage> logger)
    {
        ArgumentNullException.ThrowIfNull(callService);
        ArgumentNullException.ThrowIfNull(logger);

        InitializeComponent();
        _callService = callService;
        _logger = logger;
        Render();
    }

    /// <inheritdoc/>
    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await EndCallAsync();
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        DetailLabel.Text = string.Empty;
        _transcript.Clear();
        TranscriptLabel.Text = string.Empty;
        _muted = false;
        SetState(TutorCallState.Connecting);
        try
        {
            using var timeout = new CancellationTokenSource(ConnectTimeout);
            if (await CallView.InvokeJavaScriptAsync("createOffer", CallPageJsonContext.Default.String)
                .WaitAsync(MediaTimeout, timeout.Token) is not { } offer)
            {
                await FailAsync(MediaFailureMessage);
                return;
            }
            var answer = await _callService.StartAsync(offer, timeout.Token);
            if (answer is null)
            {
                await HangUpViewAsync();
                DetailLabel.Text = "This tutor does not offer voice calls. Check TutorClientConfig:AgentName and the "
                    + "agent's realtime settings.";
                SetState(TutorCallState.Disconnected);
                return;
            }
            if (!await CallView.InvokeJavaScriptAsync(
                "acceptAnswer", CallPageJsonContext.Default.Boolean, [answer], [CallPageJsonContext.Default.String])
                .WaitAsync(MediaTimeout, timeout.Token))
            {
                await FailAsync(MediaFailureMessage);
                return;
            }
            SetState(TutorCallState.Listening);
        }
        catch (OptionsValidationException ex)
        {
            _logger.LogWarning(ex, "{ClassName} tutor settings failed validation for {OptionsName}",
                nameof(ConversationPage), ex.OptionsType.Name);
            await FailAsync("Tutor settings are incomplete. Enter them on the Settings tab, or in user secrets on Windows.");
        }
        catch (MsalException ex)
        {
            _logger.LogWarning(ex, "{ClassName} sign-in failed", nameof(ConversationPage));
            await FailAsync("Sign-in failed or was cancelled.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "{ClassName} call setup failed", nameof(ConversationPage));
            await FailAsync(ex.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Your account is not allowed to start tutor calls.",
                HttpStatusCode.TooManyRequests => "The tutor is busy with other calls; please try again shortly.",
                _ => "Could not start the call through the Agent Runtime.",
            });
        }
        catch (OperationCanceledException)
        {
            await FailAsync("Connecting took too long; please try again.");
        }
        catch (TimeoutException)
        {
            await FailAsync(MediaFailureMessage);
        }
    }

    private async void OnMuteClicked(object? sender, EventArgs e)
    {
        _muted = !_muted;
        await CallView.InvokeJavaScriptAsync(
            "setMuted", CallPageJsonContext.Default.Boolean, [_muted], [CallPageJsonContext.Default.Boolean]);
        Render();
    }

    private async void OnEndClicked(object? sender, EventArgs e)
    {
        EndButton.IsEnabled = false;
        await EndCallAsync();
    }

    private void OnCallViewRawMessageReceived(object? sender, HybridWebViewRawMessageReceivedEventArgs e)
    {
        if (e.Message is null)
            return;
        using var message = JsonDocument.Parse(e.Message);
        var root = message.RootElement;
        switch (root.GetProperty("type").GetString())
        {
            case "transcript":
                AddTranscript(root.GetProperty("speaker").GetString() == "tutor" ? "Tutor" : "You",
                    root.GetProperty("text").GetString() ?? string.Empty);
                break;
            case "state" when _state is TutorCallState.Listening or TutorCallState.Speaking:
                var value = root.GetProperty("value").GetString();
                if (value == "failed")
                    _ = EndCallAsync();
                else
                    SetState(value == "speaking" ? TutorCallState.Speaking : TutorCallState.Listening);
                break;
        }
    }

    private void OnCallViewInitialized(object? sender, WebViewInitializedEventArgs e) => AllowMicrophone(e);

    /// <summary>Lets the hosted call page open the microphone.</summary>
    /// <param name="e">The platform web view initialization arguments.</param>
    partial void AllowMicrophone(WebViewInitializedEventArgs e);

    // Also runs when media drops, so the runtime's summary explains a hang-up it made itself.
    private async Task EndCallAsync()
    {
        // Ask the runtime first so it records the learner's request before the provider sees media close.
        var ending = _callService.EndAsync(CancellationToken.None);
        await HangUpViewAsync();
        var summary = await ending;
        if (_state is TutorCallState.Listening or TutorCallState.Speaking or TutorCallState.Connecting)
            DetailLabel.Text = summary is null ? "The call connection was lost." : Describe(summary);
        if (_state != TutorCallState.Error)
            SetState(TutorCallState.Disconnected);
    }

    private static string Describe(RealtimeCallSummaryResponse summary)
    {
        var ending = summary.EndReason switch
        {
            RealtimeCallEndReason.ClientRequested => "Call ended.",
            RealtimeCallEndReason.MaximumDuration => "The call reached its time limit.",
            RealtimeCallEndReason.HostShutdown => "The tutor service restarted and ended the call.",
            _ => "The call connection was lost.",
        };
        var cost = summary.EstimatedCost is { } estimate ? $", about {estimate.Amount:0.00} {estimate.Currency}" : string.Empty;
        return $"{ending} It lasted {summary.DurationSeconds / 60}:{summary.DurationSeconds % 60:00} and used "
            + $"{summary.Usage.TotalTokens:N0} tokens{cost}.";
    }

    private async Task FailAsync(string message)
    {
        await HangUpViewAsync();
        await _callService.EndAsync(CancellationToken.None);
        DetailLabel.Text = message;
        SetState(TutorCallState.Error);
    }

    private async Task HangUpViewAsync()
    {
        try
        {
            await CallView.InvokeJavaScriptAsync("hangUp", CallPageJsonContext.Default.Boolean).WaitAsync(HangUpTimeout);
        }
        catch (TimeoutException ex)
        {
            // Ending the runtime call matters more than confirming the web view released the microphone.
            _logger.LogWarning(ex, "{ClassName} web view did not confirm hang-up", nameof(ConversationPage));
        }
    }

    private void AddTranscript(string speaker, string text)
    {
        _transcript.Enqueue($"{speaker}: {text}");
        while (_transcript.Count > MaxTranscriptLines)
            _transcript.Dequeue();
        TranscriptLabel.Text = string.Join(Environment.NewLine + Environment.NewLine, _transcript);
        _ = TranscriptScroll.ScrollToAsync(TranscriptLabel, ScrollToPosition.End, animated: false);
    }

    private void SetState(TutorCallState state)
    {
        _state = state;
        Render();
    }

    private void Render()
    {
        var active = _state is TutorCallState.Listening or TutorCallState.Speaking;
        StateLabel.Text = _muted && active ? $"{_state} (muted)" : _state.ToString();
        StartButton.IsEnabled = _state is TutorCallState.Disconnected or TutorCallState.Error;
        MuteButton.IsEnabled = active;
        MuteButton.Text = _muted ? "Unmute" : "Mute";
        EndButton.IsEnabled = active || _state == TutorCallState.Connecting;
    }
}
