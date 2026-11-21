namespace CasCap.Services;

/// <summary>Holds the retention choice for new calls until the app closes.</summary>
public sealed class VoiceCallPreferences
{
    /// <summary>Gets or sets whether to request text retention for new calls.</summary>
    public bool RetainNewCalls { get; set; }
}
