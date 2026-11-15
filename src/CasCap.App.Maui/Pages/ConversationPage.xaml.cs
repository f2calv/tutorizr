using CasCap.AgentRuntime.Contracts.V1;
using Microsoft.Identity.Client;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
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
    private const string MicrophonePulse = nameof(MicrophonePulse);
    private const string MediaFailureMessage =
        "Could not open the microphone or start call audio. Check the app's microphone permission.";

    private readonly TutorCallService _callService;
    private readonly ILogger<ConversationPage> _logger;
    private readonly Queue<string> _transcript = new();
    private TutorCallState _state = TutorCallState.Disconnected;
    private bool _muted;
    private bool _hearing;
    private bool _foreground = true;
    private bool _authenticating;
    private CancellationTokenSource? _connecting;
    private Task? _starting;
    private Task? _ending;

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
    protected override void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _callService.RestoreConversation();
            Render();
        }
        catch (OptionsValidationException)
        {
            DetailLabel.Text = "Enter the tutor connection settings before starting a call.";
        }
    }

    /// <summary>Updates the window's foreground status without treating tab navigation as a hang-up.</summary>
    /// <param name="foreground">Whether this app window is active.</param>
    public void SetForeground(bool foreground)
    {
        _foreground = foreground;
        UpdateKeepAwake();
    }

    /// <summary>Ends media when the window stops; resumption always requires an explicit learner action.</summary>
    public async Task SuspendAsync()
    {
        SetForeground(false);
        // The system browser must be allowed to finish sign-in before the call opens its microphone.
        if (!_authenticating && _state is TutorCallState.Connecting or TutorCallState.Listening
            or TutorCallState.Speaking or TutorCallState.Reconnecting or TutorCallState.Disconnecting)
        {
            await EndCallAsync();
            DetailLabel.Text += _callService.CanResume
                ? " App is inactive. Press Resume when you return to continue your conversation."
                : " App is inactive. Start a new call when you return.";
            SetState(TutorCallState.Suspended);
        }
    }

    private async void OnStartClicked(object? sender, EventArgs e)
        => await BeginCallAsync(resume: false);

    private async void OnResumeClicked(object? sender, EventArgs e)
        => await BeginCallAsync(resume: true);

    private async Task BeginCallAsync(bool resume)
    {
        if (_starting is { IsCompleted: false } || _ending is { IsCompleted: false })
            return;
        if (resume)
            RememberConversationSwitch.IsToggled = true;
        _starting = StartCallAsync(resume);
        await _starting;
    }

    private async Task StartCallAsync(bool resume)
    {
        DetailLabel.Text = string.Empty;
        if (!resume)
        {
            _transcript.Clear();
            TranscriptLabel.Text = string.Empty;
        }
        _muted = false;
        SetState(TutorCallState.Connecting);
        using var timeout = new CancellationTokenSource(ConnectTimeout);
        _connecting = timeout;
        try
        {
            _authenticating = true;
            UpdateKeepAwake();
            try
            {
                await _callService.PrepareAsync(timeout.Token);
            }
            finally
            {
                _authenticating = false;
                UpdateKeepAwake();
            }
            timeout.Token.ThrowIfCancellationRequested();
            if (!_foreground)
            {
                DetailLabel.Text = "Return to the app and connect when you are ready.";
                SetState(TutorCallState.Suspended);
                return;
            }
            if (await CallView.InvokeJavaScriptAsync("createOffer", CallPageJsonContext.Default.String)
                .WaitAsync(MediaTimeout, timeout.Token) is not { } offer)
            {
                await FailAsync(MediaFailureMessage);
                return;
            }
            var answer = await _callService.StartAsync(
                offer, timeout.Token, resume, RememberConversationSwitch.IsToggled);
            timeout.Token.ThrowIfCancellationRequested();
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
            timeout.Token.ThrowIfCancellationRequested();
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
                HttpStatusCode.Conflict => "Resume is unavailable: the conversation expired, belongs to another account, "
                    + "is busy, its definition changed, or knowledge tools are enabled. Start New with retention off "
                    + "for a non-retained call.",
                _ => "Could not start the call through the Agent Runtime.",
            });
        }
        catch (OperationCanceledException)
        {
            if (_state != TutorCallState.Disconnecting)
                await FailAsync("Connecting took too long; please try again.");
        }
        catch (TimeoutException)
        {
            await FailAsync(MediaFailureMessage);
        }
        finally
        {
            _connecting = null;
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

    // Android raises web view messages on a background thread; the page may only be updated from the main thread.
    private void OnCallViewRawMessageReceived(object? sender, HybridWebViewRawMessageReceivedEventArgs e)
    {
        if (e.Message is { } message)
            Dispatcher.Dispatch(() => HandleCallViewMessage(message));
    }

    private async void HandleCallViewMessage(string json)
    {
        using var message = JsonDocument.Parse(json);
        var root = message.RootElement;
        switch (root.GetProperty("type").GetString())
        {
            case "transcript":
                AddTranscript(root.GetProperty("speaker").GetString() == "tutor" ? "Tutor" : "You",
                    root.GetProperty("text").GetString() ?? string.Empty);
                break;
            case "microphone" when _state is TutorCallState.Listening or TutorCallState.Speaking:
                SetHearing(root.GetProperty("hearing").GetBoolean());
                break;
            case "state" when _state is TutorCallState.Listening or TutorCallState.Speaking
                or TutorCallState.Reconnecting or TutorCallState.Connecting:
                var value = root.GetProperty("value").GetString();
                if (value == "failed")
                    await EndCallAsync(connectionLost: true);
                else if (_state == TutorCallState.Connecting)
                    break;
                else if (value == "reconnecting")
                    SetState(TutorCallState.Reconnecting);
                else if (_state != TutorCallState.Reconnecting || value == "connected")
                    SetState(value == "speaking" ? TutorCallState.Speaking : TutorCallState.Listening);
                break;
        }
    }

    private void OnCallViewInitialized(object? sender, WebViewInitializedEventArgs e) => AllowMicrophone(e);

    /// <summary>Lets the hosted call page open the microphone.</summary>
    /// <param name="e">The platform web view initialization arguments.</param>
    partial void AllowMicrophone(WebViewInitializedEventArgs e);

    // Also runs when media drops, so the runtime's summary explains a hang-up it made itself.
    private Task EndCallAsync(bool connectionLost = false)
    {
        if (_ending is not { IsCompleted: false })
            _ending = EndCallCoreAsync(connectionLost);
        return _ending;
    }

    private async Task EndCallCoreAsync(bool connectionLost)
    {
        var wasActive = _state is TutorCallState.Listening or TutorCallState.Speaking or TutorCallState.Connecting
            or TutorCallState.Reconnecting;
        SetState(TutorCallState.Disconnecting);
        if (_connecting is { } connecting)
            await connecting.CancelAsync();
        if (_starting is { IsCompleted: false } starting)
            await starting;
        // Ask the runtime first so it records the learner's request before the provider sees media close.
        var ending = _callService.EndAsync(CancellationToken.None);
        await HangUpViewAsync();
        var summary = await ending;
        if (wasActive)
            DetailLabel.Text = summary is null ? "The call connection was lost."
                : (connectionLost ? "The call connection was lost. " : string.Empty) + Describe(summary);
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
        UpdateKeepAwake(ending: true);
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
        catch (Exception ex) when (ex is TimeoutException or ObjectDisposedException)
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
        UpdateKeepAwake();
        if (state is not (TutorCallState.Listening or TutorCallState.Speaking))
            SetHearing(false);
        Render();
    }

    private void UpdateKeepAwake(bool ending = false) =>
        DeviceDisplay.Current.KeepScreenOn = !ending && _foreground && !_authenticating
            && _state is TutorCallState.Connecting or TutorCallState.Listening
                or TutorCallState.Speaking or TutorCallState.Reconnecting;

    private void SetHearing(bool hearing)
    {
        _hearing = hearing;
        this.AbortAnimation(MicrophonePulse);
        MicrophoneDot.Scale = 1;
        MicrophoneDot.Fill = new SolidColorBrush(hearing ? Colors.LimeGreen : Colors.Gray);
        if (hearing)
            new Animation(scale => MicrophoneDot.Scale = scale, 1, 1.5, Easing.SinInOut)
                .Commit(this, MicrophonePulse, length: 500, finished: (_, _) => MicrophoneDot.Scale = 1, repeat: () => _hearing);
        Render();
    }

    private void Render()
    {
        var active = _state is TutorCallState.Listening or TutorCallState.Speaking;
        StateLabel.Text = _muted && active ? $"{_state} (muted)" : _state.ToString();
        StartButton.IsEnabled = _state is TutorCallState.Disconnected or TutorCallState.Error or TutorCallState.Suspended;
        ResumeButton.IsEnabled = StartButton.IsEnabled && _callService.CanResume;
        RememberConversationSwitch.IsEnabled = StartButton.IsEnabled;
        MuteButton.IsEnabled = active;
        MuteButton.Text = _muted ? "Unmute" : "Mute";
        EndButton.IsEnabled = active || _state is TutorCallState.Connecting or TutorCallState.Reconnecting;
        MicrophoneIndicator.IsVisible = active;
        MicrophoneLabel.Text = _muted ? "Microphone muted" : _hearing ? "Hearing you" : "Microphone on, quiet";
        SemanticProperties.SetDescription(MicrophoneIndicator, MicrophoneLabel.Text);
    }
}
