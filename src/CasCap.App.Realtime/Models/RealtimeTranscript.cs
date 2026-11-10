namespace CasCap.Models;

/// <summary>A transient transcript fragment for display only; it is never persisted.</summary>
/// <param name="Speaker">Who spoke.</param>
/// <param name="Text">Transcript text, or a delta when <paramref name="IsFinal"/> is <see langword="false"/>.</param>
/// <param name="IsFinal">Whether <paramref name="Text"/> is the complete utterance.</param>
public sealed record RealtimeTranscript(RealtimeSpeaker Speaker, string Text, bool IsFinal);
