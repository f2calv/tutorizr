namespace CasCap.Models;

/// <summary>Visible lifecycle state of a voice call.</summary>
public enum TutorCallState
{
    /// <summary>No call is active.</summary>
    Disconnected,

    /// <summary>Signing in, opening the microphone and negotiating the call.</summary>
    Connecting,

    /// <summary>The call is waiting for, or hearing, the learner.</summary>
    Listening,

    /// <summary>Tutor audio is playing.</summary>
    Speaking,

    /// <summary>The call failed and has been closed.</summary>
    Error,
}
