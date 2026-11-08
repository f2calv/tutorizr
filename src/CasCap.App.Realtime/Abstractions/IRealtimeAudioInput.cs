namespace CasCap.Abstractions;

/// <summary>Continuous microphone capture in the negotiated realtime format.</summary>
public interface IRealtimeAudioInput
{
    /// <summary>Captures 16-bit little-endian mono PCM at <see cref="RealtimeAudioFormat.SampleRate"/> until cancelled.</summary>
    /// <param name="cancellationToken">Stops capture and releases the device.</param>
    /// <returns>Raw PCM frames without headers.</returns>
    public IAsyncEnumerable<ReadOnlyMemory<byte>> CaptureAsync(CancellationToken cancellationToken);
}
