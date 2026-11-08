namespace CasCap.Abstractions;

/// <summary>Streamed playback of 16-bit little-endian mono PCM at <see cref="RealtimeAudioFormat.SampleRate"/>.</summary>
public interface IRealtimeAudioOutput
{
    /// <summary>Gets whether queued audio remains to be played.</summary>
    public bool IsPlaying { get; }

    /// <summary>Opens the output device.</summary>
    public void Start();

    /// <summary>Queues audio for immediate playback.</summary>
    /// <param name="pcm">Raw PCM without headers.</param>
    public void Enqueue(byte[] pcm);

    /// <summary>Discards queued audio so the learner stops hearing it.</summary>
    /// <returns>The number of queued bytes that were never played.</returns>
    public int Clear();

    /// <summary>Closes the output device.</summary>
    public void Stop();
}
