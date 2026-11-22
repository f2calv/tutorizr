namespace CasCap.Models;

/// <summary>A transient learner or tutor utterance displayed in the current conversation.</summary>
public sealed record TranscriptMessage
{
    /// <summary>Gets the human-readable speaker label.</summary>
    public required string Speaker { get; init; }

    /// <summary>Gets the utterance text, which is never written to operational logs or local storage.</summary>
    public required string Text { get; init; }

    /// <summary>Gets whether the utterance belongs to the learner.</summary>
    public bool IsLearner { get; init; }
}
