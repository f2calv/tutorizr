using Azure.Core;
using CasCap.Exceptions;
using System.Buffers;
using System.Net.WebSockets;

namespace CasCap.Services;

/// <summary>
/// Runs one live tutor conversation over the Azure OpenAI GA Realtime WebSocket: streams microphone audio, plays
/// tutor audio as it arrives and, when the learner interrupts, truncates the reply to what was actually heard.
/// </summary>
/// <remarks>Transcripts and audio are only raised as events for display; they are never logged or stored.</remarks>
public sealed class RealtimeConversation(
    IOptions<RealtimeConfig> options,
    TokenCredential credential,
    IRealtimeAudioInput input,
    IRealtimeAudioOutput output,
    ILogger<RealtimeConversation> logger) : IAsyncDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly Lock _playbackLock = new();
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _sessionCts;
    private TaskCompletionSource? _sessionReady;
    private Task? _receiveTask;
    private Task? _captureTask;
    private string? _playingItemId;
    private string? _interruptedItemId;
    private int _playingItemBytes;
    private volatile bool _responseDone = true;
    private int _stopping;

    /// <summary>Raised from a background thread when <see cref="State"/> changes.</summary>
    public event EventHandler<RealtimeConversationState>? StateChanged;

    /// <summary>Raised from a background thread with transient transcript text.</summary>
    public event EventHandler<RealtimeTranscript>? TranscriptReceived;

    /// <summary>Raised from a background thread with a learner-safe message when the service reports a recoverable problem.</summary>
    public event EventHandler<string>? Notice;

    /// <summary>Raised from a background thread with a learner-safe message when the conversation fails and closes.</summary>
    public event EventHandler<string>? Faulted;

    /// <summary>Gets the current lifecycle state.</summary>
    public RealtimeConversationState State { get; private set; }

    /// <summary>Gets whether microphone audio is withheld from the session.</summary>
    public bool IsMuted { get; private set; }

    /// <summary>Authenticates, opens the session and starts streaming once the session confirms its configuration.</summary>
    /// <param name="cancellationToken">Cancels connection setup only; use <see cref="StopAsync"/> to end the conversation.</param>
    /// <exception cref="InvalidOperationException">A conversation is already active.</exception>
    /// <exception cref="OptionsValidationException">The realtime configuration is incomplete.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (State is not (RealtimeConversationState.Disconnected or RealtimeConversationState.Error))
            throw new InvalidOperationException("A conversation is already active.");

        var config = options.Value;
        SetState(RealtimeConversationState.Connecting);
        Volatile.Write(ref _stopping, 0);
        _responseDone = true;
        IsMuted = false;
        ResetPlayback();

        var socket = new ClientWebSocket();
        try
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext([config.TokenScope]), cancellationToken);
            socket.Options.SetRequestHeader("Authorization", $"Bearer {token.Token}");
            await socket.ConnectAsync(BuildUri(config), cancellationToken);
        }
        catch
        {
            socket.Dispose();
            SetState(RealtimeConversationState.Disconnected);
            throw;
        }

        _socket = socket;
        _sessionCts = new CancellationTokenSource();
        _sessionReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await SendAsync(RealtimeClientEvents.SessionUpdate(config), cancellationToken);
            output.Start();
        }
        catch
        {
            await StopCoreAsync(RealtimeConversationState.Disconnected);
            throw;
        }

        var sessionToken = _sessionCts.Token;
        _receiveTask = Task.Run(() => RunAsync(ReceiveLoopAsync, sessionToken), CancellationToken.None);
        _captureTask = Task.Run(() => RunAsync(CaptureLoopAsync, sessionToken), CancellationToken.None);
        logger.LogInformation("Realtime conversation connected");
    }

    /// <summary>Withholds or resumes microphone audio; muting also discards learner audio not yet committed.</summary>
    /// <param name="muted">Whether to mute.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task SetMutedAsync(bool muted, CancellationToken cancellationToken)
    {
        IsMuted = muted;
        if (muted && _socket?.State == WebSocketState.Open)
            await SendAsync(RealtimeClientEvents.ClearInputAudio(), cancellationToken);
        StateChanged?.Invoke(this, State);
    }

    /// <summary>Ends the conversation, closing the session, microphone and speaker.</summary>
    public Task StopAsync() => StopCoreAsync(RealtimeConversationState.Disconnected);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _sendLock.Dispose();
    }

    private static Uri BuildUri(RealtimeConfig config) => new UriBuilder(config.Endpoint!)
    {
        Scheme = "wss",
        Port = -1,
        Path = "openai/v1/realtime",
        Query = $"model={Uri.EscapeDataString(config.Deployment)}",
    }.Uri;

    private async Task RunAsync(Func<CancellationToken, Task> loop, CancellationToken cancellationToken)
    {
        try
        {
            await loop(cancellationToken);
        }
        catch (Exception ex) when (Volatile.Read(ref _stopping) == 0 && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Realtime conversation failed");
            Faulted?.Invoke(this, Describe(ex));
            _ = StopCoreAsync(RealtimeConversationState.Error);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
            // Expected while the session is being torn down.
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new ArrayBufferWriter<byte>(64 * 1024);
        while (true)
        {
            buffer.ResetWrittenCount();
            ValueWebSocketReceiveResult result;
            do
            {
                result = await _socket!.ReceiveAsync(buffer.GetMemory(16 * 1024), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    logger.LogWarning("Realtime service closed the session with {CloseStatus}", _socket.CloseStatus);
                    throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
                }
                buffer.Advance(result.Count);
            }
            while (!result.EndOfMessage);

            await HandleServerEventAsync(buffer.WrittenMemory, cancellationToken);
        }
    }

    private async Task CaptureLoopAsync(CancellationToken cancellationToken)
    {
        await _sessionReady!.Task.WaitAsync(cancellationToken);
        await foreach (var frame in input.CaptureAsync(cancellationToken))
        {
            if (State == RealtimeConversationState.Speaking && _responseDone && !output.IsPlaying)
                SetState(RealtimeConversationState.Listening);
            if (IsMuted)
                continue;
            await SendAsync(RealtimeClientEvents.AppendAudio(frame.Span), cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        throw new RealtimeAudioDeviceException("The microphone stopped delivering audio.");
    }

    private async Task HandleServerEventAsync(ReadOnlyMemory<byte> json, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var type = root.GetProperty("type").GetString();
        switch (type)
        {
            case "session.updated":
                if (_sessionReady!.TrySetResult())
                    SetState(RealtimeConversationState.Listening);
                break;
            case "input_audio_buffer.speech_started":
                await InterruptPlaybackAsync(cancellationToken);
                SetState(RealtimeConversationState.Listening);
                break;
            case "input_audio_buffer.speech_stopped":
                SetState(RealtimeConversationState.Thinking);
                break;
            case "response.created":
                _responseDone = false;
                break;
            case "response.output_audio.delta":
                OnAudioDelta(root);
                break;
            case "response.output_audio_transcript.delta":
                RaiseTranscript(RealtimeSpeaker.Tutor, root, "delta", isFinal: false);
                break;
            case "response.output_audio_transcript.done":
                RaiseTranscript(RealtimeSpeaker.Tutor, root, "transcript", isFinal: true);
                break;
            case "conversation.item.input_audio_transcription.completed":
                RaiseTranscript(RealtimeSpeaker.Learner, root, "transcript", isFinal: true);
                break;
            case "response.done":
                OnResponseDone(root);
                break;
            case "error":
                var error = root.GetProperty("error");
                var code = error.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : null;
                logger.LogWarning("Realtime service reported error {ErrorCode}", code);
                Notice?.Invoke(this, $"The service reported a problem ({code ?? "unknown"}).");
                break;
            default:
                break;
        }
    }

    private void OnAudioDelta(JsonElement root)
    {
        var itemId = root.GetProperty("item_id").GetString();
        var pcm = root.GetProperty("delta").GetBytesFromBase64();
        lock (_playbackLock)
        {
            if (itemId == _interruptedItemId)
                return;
            if (itemId != _playingItemId)
            {
                _playingItemId = itemId;
                _playingItemBytes = 0;
            }
            _playingItemBytes += pcm.Length;
            output.Enqueue(pcm);
        }
        SetState(RealtimeConversationState.Speaking);
    }

    private void OnResponseDone(JsonElement root)
    {
        _responseDone = true;
        var response = root.GetProperty("response");
        if (response.TryGetProperty("status", out var status) && status.GetString() == "failed")
        {
            logger.LogWarning("Realtime response failed");
            Notice?.Invoke(this, "The tutor could not reply; please try again.");
        }
        if (State == RealtimeConversationState.Thinking)
            SetState(RealtimeConversationState.Listening);
    }

    private async Task InterruptPlaybackAsync(CancellationToken cancellationToken)
    {
        string? itemId;
        int unplayed;
        int played;
        lock (_playbackLock)
        {
            unplayed = output.Clear();
            itemId = _playingItemId;
            played = Math.Max(0, _playingItemBytes - unplayed);
            _interruptedItemId = itemId ?? _interruptedItemId;
            _playingItemId = null;
            _playingItemBytes = 0;
        }
        if (itemId is not null && unplayed > 0)
            await SendAsync(RealtimeClientEvents.TruncateItem(itemId, played / RealtimeAudioFormat.BytesPerMillisecond), cancellationToken);
    }

    private void RaiseTranscript(RealtimeSpeaker speaker, JsonElement root, string property, bool isFinal)
    {
        if (TranscriptReceived is not null && root.TryGetProperty(property, out var text) && text.GetString() is { Length: > 0 } value)
            TranscriptReceived(this, new RealtimeTranscript(speaker, value, isFinal));
    }

    private async Task SendAsync(ReadOnlyMemory<byte> json, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _socket!.SendAsync(json, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task StopCoreAsync(RealtimeConversationState finalState)
    {
        if (Interlocked.Exchange(ref _stopping, 1) == 1 || _socket is null)
            return;

        var socket = _socket;
        if (socket.State == WebSocketState.Open)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ended", timeout.Token);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
            {
                // The service may already have dropped the connection.
            }
        }
        await _sessionCts!.CancelAsync();
        _sessionReady?.TrySetCanceled();
        try
        {
            await Task.WhenAll(_receiveTask ?? Task.CompletedTask, _captureTask ?? Task.CompletedTask).WaitAsync(StopTimeout);
        }
        catch (TimeoutException)
        {
            logger.LogWarning("Realtime conversation loops did not stop within {StopTimeout}", StopTimeout);
        }

        output.Stop();
        ResetPlayback();
        socket.Dispose();
        _sessionCts.Dispose();
        _socket = null;
        _sessionCts = null;
        _receiveTask = null;
        _captureTask = null;
        IsMuted = false;
        SetState(finalState);
        logger.LogInformation("Realtime conversation ended");
    }

    private void ResetPlayback()
    {
        lock (_playbackLock)
        {
            _playingItemId = null;
            _interruptedItemId = null;
            _playingItemBytes = 0;
        }
    }

    private void SetState(RealtimeConversationState state)
    {
        if (State == state)
            return;
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private static string Describe(Exception ex) => ex switch
    {
        RealtimeAudioDeviceException => ex.Message,
        WebSocketException => "The connection to Azure OpenAI was lost.",
        _ => "The conversation stopped unexpectedly.",
    };
}
