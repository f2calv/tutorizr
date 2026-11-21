using CasCap.AgentRuntime.Contracts.V1;
using Microsoft.Identity.Client;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private static readonly TimeSpan SpeechPaceTimeout = TimeSpan.FromSeconds(20);
    private const int MaxTranscriptLines = 40;
    private const string MicrophonePulse = nameof(MicrophonePulse);
    private const string MediaFailureMessage =
        "Could not open the microphone or start call audio. Check the app's microphone permission.";

    private readonly TutorCallService _callService;
    private readonly ILogger<ConversationPage> _logger;
    private readonly VoiceCallPreferences _voicePreferences;
    private readonly CallDiagnosticLog _diagnosticLog;
    private readonly ObservableCollection<string> _transcript = [];
    private TutorCallState _state = TutorCallState.Disconnected;
    private bool _muted;
    private bool _hearing;
    private bool _foreground = true;
    private bool _authenticating;
    private bool _changingSpeechPace;
    private int _speechPaceGeneration;
    private CancellationTokenSource? _connecting;
    private Task? _starting;
    private Task? _ending;

    /// <summary>Initializes a new instance of the <see cref="ConversationPage"/> class.</summary>
    /// <param name="callService">Starts and ends calls through the Agent Runtime.</param>
    /// <param name="logger">The page logger.</param>
    /// <param name="voicePreferences">The current app session's voice preferences.</param>
    /// <param name="diagnosticLog">The local call setup log.</param>
    public ConversationPage(
        TutorCallService callService, ILogger<ConversationPage> logger,
        VoiceCallPreferences voicePreferences, CallDiagnosticLog diagnosticLog)
    {
        ArgumentNullException.ThrowIfNull(callService);
        ArgumentNullException.ThrowIfNull(logger);

        InitializeComponent();
        _callService = callService;
        _logger = logger;
        _voicePreferences = voicePreferences;
        _diagnosticLog = diagnosticLog;
        TranscriptView.ItemsSource = _transcript;
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
        _starting = StartCallAsync(resume, resume || _voicePreferences.RetainNewCalls);
        await _starting;
    }

    private async Task StartCallAsync(bool resume, bool retain)
    {
        var started = Stopwatch.GetTimestamp();
        var stage = "Sign-in";
        var outcome = "Setup failed";
        Exception? error = null;
        DetailLabel.Text = string.Empty;
        if (!resume)
            _transcript.Clear();
        _muted = false;
        _speechPaceGeneration++;
        _changingSpeechPace = false;
        SpeechPaceLabel.Text = string.Empty;
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
                outcome = "App left the foreground";
                DetailLabel.Text = "Return to the app and connect when you are ready.";
                SetState(TutorCallState.Suspended);
                return;
            }
            stage = "Microphone and audio offer";
            if (await CallView.InvokeJavaScriptAsync("createOffer", CallPageJsonContext.Default.String)
                .WaitAsync(MediaTimeout, timeout.Token) is not { } offer)
            {
                await FailAsync(MediaFailureMessage);
                return;
            }
            stage = "Agent Runtime call setup";
            var answer = await _callService.StartAsync(offer, timeout.Token, resume, retain);
            timeout.Token.ThrowIfCancellationRequested();
            if (answer is null)
            {
                outcome = "Voice call unavailable";
                await FailAsync("This tutor does not offer voice calls. Check the tutor name and its realtime settings.");
                return;
            }
            stage = "Audio connection";
            if (!await CallView.InvokeJavaScriptAsync(
                "acceptAnswer", CallPageJsonContext.Default.Boolean, [answer], [CallPageJsonContext.Default.String])
                .WaitAsync(MediaTimeout, timeout.Token))
            {
                await FailAsync(MediaFailureMessage);
                return;
            }
            timeout.Token.ThrowIfCancellationRequested();
            SetState(TutorCallState.Listening);
            outcome = "Setup completed";
        }
        catch (OptionsValidationException ex)
        {
            error = ex;
            await FailAsync("Tutor settings are incomplete. Enter them on the Settings tab, or in user secrets on Windows.");
        }
        catch (MsalException ex)
        {
            error = ex;
            await FailAsync("Sign-in failed or was cancelled.");
        }
        catch (HttpRequestException ex)
        {
            error = ex;
            await FailAsync(ex.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Your account is not allowed to start tutor calls.",
                HttpStatusCode.TooManyRequests => "The tutor is busy with other calls; please try again shortly.",
                HttpStatusCode.Conflict when resume => "The runtime rejected Resume. The conversation may be expired, "
                    + "busy, tied to another account, or incompatible with the current tutor or collection policy. Try New with retention off.",
                HttpStatusCode.Conflict when retain => "The runtime rejected retention for this new call. "
                    + "Check tutor capabilities in Settings, or turn retention off and try New.",
                _ => "Could not start the call through the Agent Runtime. See Technical Log for details.",
            });
        }
        catch (OperationCanceledException ex)
        {
            error = ex;
            if (_state != TutorCallState.Disconnecting)
                await FailAsync("Connecting took too long; please try again.");
            else
                outcome = "Setup cancelled";
        }
        catch (TimeoutException ex)
        {
            error = ex;
            await FailAsync(MediaFailureMessage);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException)
        {
            error = ex;
            await FailAsync("The call setup response could not be read. See Technical Log for details.");
        }
        finally
        {
            _connecting = null;
            _diagnosticLog.RecordSetup(resume, retain, Stopwatch.GetElapsedTime(started), stage, outcome, error);
        }
    }

    private async void OnCopyErrorClicked(object? sender, EventArgs e)
    {
        try
        {
            await Clipboard.Default.SetTextAsync(DetailLabel.Text);
        }
        catch (InvalidOperationException)
        {
            await DisplayAlertAsync("Could not copy", "The clipboard is unavailable.", "OK");
        }
    }

    private async void OnMuteClicked(object? sender, EventArgs e)
    {
        _muted = !_muted;
        await CallView.InvokeJavaScriptAsync(
            "setMuted", CallPageJsonContext.Default.Boolean, [_muted], [CallPageJsonContext.Default.Boolean]);
        Render();
    }

    private async void OnSpeechPaceClicked(object? sender, EventArgs e)
    {
        if (_changingSpeechPace || _state is not (TutorCallState.Listening or TutorCallState.Speaking)
            || sender is not Button { CommandParameter: string pace })
            return;

        var speed = pace switch
        {
            "Slow" => 0.8,
            "Normal" => 1.0,
            "Fast" => 1.2,
            _ => throw new InvalidOperationException("Unknown speech pace control."),
        };
        var generation = _speechPaceGeneration;
        _changingSpeechPace = true;
        SpeechPaceLabel.Text = $"Requesting {pace.ToLowerInvariant()} pace between replies...";
        Render();
        var applied = false;
        try
        {
            applied = await CallView.InvokeJavaScriptAsync(
                "setSpeechSpeed", CallPageJsonContext.Default.Boolean, [speed], [CallPageJsonContext.Default.Double])
                .WaitAsync(SpeechPaceTimeout);
        }
        catch (Exception ex) when (ex is TimeoutException or ObjectDisposedException or InvalidOperationException or JsonException)
        {
            if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Warning))
                _logger.LogWarning("{ClassName} could not confirm the speech pace change", nameof(ConversationPage));
        }
        if (generation != _speechPaceGeneration)
            return;

        _changingSpeechPace = false;
        SpeechPaceLabel.Text = applied ? $"Current pace: {pace}" : "The tutor did not confirm the pace change. Try again.";
        Render();
    }

    private async void OnEndClicked(object? sender, EventArgs e)
    {
        EndButton.IsEnabled = false;
        await EndCallAsync();
    }

    // Android raises web view messages on a background thread; the page may only be updated from the main thread.
    private async void OnCallViewRawMessageReceived(object? sender, HybridWebViewRawMessageReceivedEventArgs e)
    {
        if (e.Message is { } message)
            await Dispatcher.DispatchAsync(() => HandleCallViewMessageAsync(message));
    }

    private async Task HandleCallViewMessageAsync(string json)
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
        _speechPaceGeneration++;
        if (_connecting is { } connecting)
            await connecting.CancelAsync();
        if (_starting is { IsCompleted: false } starting)
            await starting;
        // Ask the runtime first so it records the learner's request before the provider sees media close.
        var ending = _callService.EndAsync(CancellationToken.None);
        await HangUpViewAsync();
        var summary = await ending;
        if (wasActive)
        {
            var prefix = connectionLost ? "The call connection was lost. " : string.Empty;
            DetailLabel.Text = summary is null ? "The call connection was lost." : prefix + Describe(summary);
        }
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
        if (_transcript.Count == MaxTranscriptLines)
            _transcript.RemoveAt(0);
        _transcript.Add($"{speaker}: {text}");
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
        CopyErrorButton.IsVisible = _state == TutorCallState.Error;
        MuteButton.IsEnabled = active;
        SpeechPaceControls.IsVisible = active || _state == TutorCallState.Reconnecting;
        SpeechPaceControls.IsEnabled = active && !_changingSpeechPace;
        MuteButton.Text = _muted ? "Unmute" : "Mute";
        EndButton.IsEnabled = active || _state is TutorCallState.Connecting or TutorCallState.Reconnecting;
        MicrophoneIndicator.IsVisible = active;
        MicrophoneLabel.Text = _muted ? "Microphone muted" : _hearing ? "Hearing you" : "Microphone on, quiet";
        SemanticProperties.SetDescription(MicrophoneIndicator, MicrophoneLabel.Text);
    }
}
