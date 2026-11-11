using System.Buffers;

namespace CasCap.Services;

/// <summary>Serializes the client events of the GA Realtime API (<c>/openai/v1/realtime</c>).</summary>
public static class RealtimeClientEvents
{
    /// <summary>Creates a <c>session.update</c> event configuring audio in and out, voice, instructions and turn detection.</summary>
    /// <param name="config">The session configuration.</param>
    /// <returns>The UTF-8 JSON event.</returns>
    public static ReadOnlyMemory<byte> SessionUpdate(RealtimeConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return Write(writer =>
        {
            writer.WriteString("type", "session.update");
            writer.WriteStartObject("session");
            writer.WriteString("type", "realtime");
            writer.WriteString("instructions", config.Instructions);
            writer.WriteStartArray("output_modalities");
            writer.WriteStringValue("audio");
            writer.WriteEndArray();
            writer.WriteStartObject("audio");

            writer.WriteStartObject("input");
            WritePcmFormat(writer);
            if (!string.IsNullOrWhiteSpace(config.InputTranscriptionModel))
            {
                writer.WriteStartObject("transcription");
                writer.WriteString("model", config.InputTranscriptionModel);
                if (!string.IsNullOrWhiteSpace(config.InputTranscriptionLanguage))
                    writer.WriteString("language", config.InputTranscriptionLanguage);
                writer.WriteEndObject();
            }
            writer.WriteStartObject("turn_detection");
            if (config.TurnDetection == RealtimeTurnDetection.SemanticVad)
            {
                writer.WriteString("type", "semantic_vad");
                writer.WriteString("eagerness", config.Eagerness);
            }
            else
            {
                writer.WriteString("type", "server_vad");
                writer.WriteNumber("silence_duration_ms", 700);
            }
            writer.WriteBoolean("create_response", true);
            writer.WriteBoolean("interrupt_response", true);
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteStartObject("output");
            WritePcmFormat(writer);
            writer.WriteString("voice", config.Voice);
            writer.WriteEndObject();

            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    /// <summary>Creates an <c>input_audio_buffer.append</c> event.</summary>
    /// <param name="pcm">Raw PCM in <see cref="RealtimeAudioFormat"/>.</param>
    /// <returns>The UTF-8 JSON event.</returns>
    public static ReadOnlyMemory<byte> AppendAudio(ReadOnlySpan<byte> pcm)
    {
        var writer = new ArrayBufferWriter<byte>(pcm.Length * 4 / 3 + 64);
        using (var json = new Utf8JsonWriter(writer))
        {
            json.WriteStartObject();
            json.WriteString("type", "input_audio_buffer.append");
            json.WriteBase64String("audio", pcm);
            json.WriteEndObject();
        }
        return writer.WrittenMemory;
    }

    /// <summary>Creates an <c>input_audio_buffer.clear</c> event discarding uncommitted learner audio.</summary>
    /// <returns>The UTF-8 JSON event.</returns>
    public static ReadOnlyMemory<byte> ClearInputAudio() =>
        Write(writer => writer.WriteString("type", "input_audio_buffer.clear"));

    /// <summary>
    /// Creates a <c>conversation.item.truncate</c> event so the model's conversation state holds only the tutor audio
    /// the learner actually heard.
    /// </summary>
    /// <param name="itemId">The assistant item whose audio was interrupted.</param>
    /// <param name="audioEndMs">Milliseconds of that item's audio that were played.</param>
    /// <returns>The UTF-8 JSON event.</returns>
    public static ReadOnlyMemory<byte> TruncateItem(string itemId, int audioEndMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        ArgumentOutOfRangeException.ThrowIfNegative(audioEndMs);

        return Write(writer =>
        {
            writer.WriteString("type", "conversation.item.truncate");
            writer.WriteString("item_id", itemId);
            writer.WriteNumber("content_index", 0);
            writer.WriteNumber("audio_end_ms", audioEndMs);
        });
    }

    private static void WritePcmFormat(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("format");
        writer.WriteString("type", "audio/pcm");
        writer.WriteNumber("rate", RealtimeAudioFormat.SampleRate);
        writer.WriteEndObject();
    }

    private static ReadOnlyMemory<byte> Write(Action<Utf8JsonWriter> body)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }
        return buffer.WrittenMemory;
    }
}
