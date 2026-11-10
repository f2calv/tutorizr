namespace CasCap.Models;

/// <summary>Visible lifecycle state of a realtime conversation.</summary>
public enum RealtimeConversationState
{
    /// <summary>No session is open.</summary>
    Disconnected,

    /// <summary>Authenticating and opening the session.</summary>
    Connecting,

    /// <summary>The session is waiting for, or hearing, the learner.</summary>
    Listening,

    /// <summary>The learner finished speaking and the tutor is preparing a reply.</summary>
    Thinking,

    /// <summary>Tutor audio is playing.</summary>
    Speaking,

    /// <summary>The session failed and has been closed.</summary>
    Error,
}
