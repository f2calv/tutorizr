using CasCap.Exceptions;
using NAudio;
using NAudio.Wave;

namespace CasCap.Services;

/// <summary>Plays streamed tutor audio on the default Windows output device, discarding the queue on interruption.</summary>
public sealed class WaveOutSpeaker : IRealtimeAudioOutput, IDisposable
{
    private readonly BufferedWaveProvider _queue = new(new WaveFormat(RealtimeAudioFormat.SampleRate, 16, 1), TimeSpan.FromMinutes(2))
    {
        DiscardOnBufferOverflow = true,
        ReadFully = true,
    };
    private WaveOut? _device;

    /// <inheritdoc/>
    public bool IsPlaying => _queue.BufferedBytes > 0;

    /// <inheritdoc/>
    public void Start()
    {
        if (_device is not null)
            return;
        var device = new WaveOut { BufferMilliseconds = 50, NumberOfBuffers = 2 };
        try
        {
            device.Init(_queue);
            device.Play();
        }
        catch (MmException ex)
        {
            device.Dispose();
            throw new RealtimeAudioDeviceException("No usable speaker or headset was found.", ex);
        }
        _device = device;
    }

    /// <inheritdoc/>
    public void Enqueue(byte[] pcm)
    {
        ArgumentNullException.ThrowIfNull(pcm);
        _queue.AddSamples(pcm, 0, pcm.Length);
    }

    /// <inheritdoc/>
    public int Clear()
    {
        var unplayed = _queue.BufferedBytes;
        _queue.ClearBuffer();
        return unplayed;
    }

    /// <inheritdoc/>
    public void Stop()
    {
        _device?.Stop();
        _device?.Dispose();
        _device = null;
        _queue.ClearBuffer();
    }

    /// <inheritdoc/>
    public void Dispose() => Stop();
}
