using CasCap.Exceptions;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace CasCap.Services;

/// <summary>
/// Captures the default Windows microphone through WASAPI in communications mode, which asks Windows for its echo
/// cancellation, noise suppression and gain control, and converts to the realtime PCM format.
/// </summary>
/// <param name="logger">The capture logger.</param>
public sealed class WasapiMicrophone(ILogger<WasapiMicrophone> logger) : IRealtimeAudioInput
{
    private const int FrameBytes = 40 * RealtimeAudioFormat.BytesPerMillisecond;
    private const int AccessDenied = unchecked((int)0x80070005);

    /// <inheritdoc/>
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> CaptureAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var frames = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        });
        var recorder = StartRecorder(frames.Writer);
        try
        {
            await foreach (var frame in frames.Reader.ReadAllAsync(cancellationToken))
                yield return frame;
        }
        finally
        {
            await recorder.DisposeAsync();
        }
    }

    private WasapiRecorder StartRecorder(ChannelWriter<ReadOnlyMemory<byte>> frames)
    {
        var pending = new byte[FrameBytes];
        var pendingLength = 0;
        WasapiRecorder? recorder = null;
        try
        {
            recorder = new WasapiRecorderBuilder()
                .WithCommunicationsMode()
                .WithFormat(new WaveFormat(RealtimeAudioFormat.SampleRate, 16, 1))
                .Build();
            recorder.DataAvailable += (buffer, flags, _, _) =>
            {
                var silent = (flags & AudioClientBufferFlags.Silent) != 0;
                while (!buffer.IsEmpty)
                {
                    var count = Math.Min(buffer.Length, FrameBytes - pendingLength);
                    if (silent)
                        pending.AsSpan(pendingLength, count).Clear();
                    else
                        buffer[..count].CopyTo(pending.AsSpan(pendingLength));
                    pendingLength += count;
                    buffer = buffer[count..];
                    if (pendingLength == FrameBytes)
                    {
                        frames.TryWrite(pending.ToArray());
                        pendingLength = 0;
                    }
                }
            };
            recorder.RecordingStopped += (_, e) => frames.TryComplete(
                e.Exception is null ? null : new RealtimeAudioDeviceException("The microphone stopped.", e.Exception));
            recorder.StartRecording();
            return recorder;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex.HResult == AccessDenied)
        {
            recorder?.Dispose();
            logger.LogWarning(ex, "Microphone access was denied");
            throw new RealtimeAudioDeviceException(
                "Windows blocked microphone access. Allow desktop apps to use the microphone under Settings > "
                + "Privacy & security > Microphone.", ex);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or NotSupportedException)
        {
            recorder?.Dispose();
            logger.LogWarning(ex, "Microphone could not be opened");
            throw new RealtimeAudioDeviceException("No usable microphone was found.", ex);
        }
    }
}
