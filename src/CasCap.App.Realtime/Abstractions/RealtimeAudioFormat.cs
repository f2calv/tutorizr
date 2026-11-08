namespace CasCap.Abstractions;

/// <summary>The PCM format negotiated with the realtime session in both directions.</summary>
public static class RealtimeAudioFormat
{
    /// <summary>Samples per second.</summary>
    public const int SampleRate = 24_000;

    /// <summary>Bytes in one millisecond of 16-bit mono audio.</summary>
    public const int BytesPerMillisecond = SampleRate * 2 / 1000;
}
