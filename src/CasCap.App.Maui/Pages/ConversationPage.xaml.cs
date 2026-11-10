using Azure.Identity;
using CasCap.Exceptions;
using System.Net.WebSockets;

namespace CasCap.Pages;

/// <summary>Starts, mutes and ends a live tutor conversation and shows its state and transient transcript.</summary>
public sealed partial class ConversationPage : ContentPage
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMinutes(2);
    private const int MaxTranscriptLines = 40;

    private readonly RealtimeConversation _conversation;
    private readonly ILogger<ConversationPage> _logger;
    private readonly Queue<string> _transcript = new();

    /// <summary>Initializes a new instance of the <see cref="ConversationPage"/> class.</summary>
    /// <param name="conversation">The application's single realtime conversation.</param>
    /// <param name="logger">The page logger.</param>
    public ConversationPage(RealtimeConversation conversation, ILogger<ConversationPage> logger)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(logger);

        InitializeComponent();
        _conversation = conversation;
        _logger = logger;
        _conversation.StateChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Render);
        _conversation.TranscriptReceived += (_, transcript) =>
        {
            if (transcript.IsFinal)
                MainThread.BeginInvokeOnMainThread(() => AddTranscript(transcript));
        };
        _conversation.Notice += (_, message) => MainThread.BeginInvokeOnMainThread(() => DetailLabel.Text = message);
        _conversation.Faulted += (_, message) => MainThread.BeginInvokeOnMainThread(() => DetailLabel.Text = message);
        Render();
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        DetailLabel.Text = string.Empty;
        StartButton.IsEnabled = false;
        try
        {
            using var timeout = new CancellationTokenSource(ConnectTimeout);
            await _conversation.StartAsync(timeout.Token);
        }
        catch (OptionsValidationException)
        {
            DetailLabel.Text = "Realtime configuration is incomplete. Set RealtimeConfig:Endpoint in user secrets or "
                + "the tutorizr__RealtimeConfig__Endpoint environment variable.";
        }
        catch (RealtimeAudioDeviceException ex)
        {
            DetailLabel.Text = ex.Message;
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogWarning(ex, "Azure sign-in failed");
            DetailLabel.Text = "Azure sign-in failed. Run 'az login', or complete the browser sign-in.";
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "Realtime connection failed");
            DetailLabel.Text = "Could not connect to Azure OpenAI. Check the endpoint, deployment name and that your "
                + "account has the Cognitive Services OpenAI User role.";
        }
        catch (OperationCanceledException)
        {
            DetailLabel.Text = "Connecting took too long; please try again.";
        }
        finally
        {
            Render();
        }
    }

    private async void OnMuteClicked(object? sender, EventArgs e)
    {
        try
        {
            await _conversation.SetMutedAsync(!_conversation.IsMuted, CancellationToken.None);
        }
        catch (Exception ex) when (ex is WebSocketException or InvalidOperationException or ObjectDisposedException)
        {
            _logger.LogWarning(ex, "Mute request failed");
        }
        Render();
    }

    private async void OnEndClicked(object? sender, EventArgs e)
    {
        EndButton.IsEnabled = false;
        await _conversation.StopAsync();
        Render();
    }

    private void AddTranscript(RealtimeTranscript transcript)
    {
        _transcript.Enqueue($"{(transcript.Speaker == RealtimeSpeaker.Tutor ? "Tutor" : "You")}: {transcript.Text}");
        while (_transcript.Count > MaxTranscriptLines)
            _transcript.Dequeue();
        TranscriptLabel.Text = string.Join(Environment.NewLine + Environment.NewLine, _transcript);
        _ = TranscriptScroll.ScrollToAsync(TranscriptLabel, ScrollToPosition.End, animated: false);
    }

    private void Render()
    {
        var state = _conversation.State;
        var active = state is not (RealtimeConversationState.Disconnected or RealtimeConversationState.Error);
        StateLabel.Text = _conversation.IsMuted ? $"{state} (muted)" : state.ToString();
        StartButton.IsEnabled = !active;
        MuteButton.IsEnabled = active && state != RealtimeConversationState.Connecting;
        MuteButton.Text = _conversation.IsMuted ? "Unmute" : "Mute";
        EndButton.IsEnabled = MuteButton.IsEnabled;
    }
}
