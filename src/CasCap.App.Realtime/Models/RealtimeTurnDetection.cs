namespace CasCap.Models;

/// <summary>Server-side turn detection used by a realtime session.</summary>
public enum RealtimeTurnDetection
{
    /// <summary>Detects end of turn from the meaning of the speech, tolerating pauses.</summary>
    SemanticVad,

    /// <summary>Detects end of turn from a fixed silence duration.</summary>
    ServerVad,
}
