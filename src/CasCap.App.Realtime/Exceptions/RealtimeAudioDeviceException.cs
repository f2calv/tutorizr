namespace CasCap.Exceptions;

/// <summary>A microphone or speaker failure whose message is safe to show the learner.</summary>
/// <param name="message">An actionable message without device identifiers or paths.</param>
/// <param name="innerException">The platform failure, kept for diagnostics.</param>
public sealed class RealtimeAudioDeviceException(string message, Exception? innerException = null)
    : Exception(message, innerException);
