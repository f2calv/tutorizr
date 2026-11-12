using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Storage;

namespace CasCap.Services;

/// <summary>Exposes a fixed set of configuration keys stored in MAUI <see cref="IPreferences"/>, reloadable on demand.</summary>
/// <remarks>
/// Preferences hold only non-secret settings. Empty values are omitted, so bound options keep their defaults.
/// TODO: this copies the Android DCIM Cloud Sync provider; move both into a shared MAUI library if a third app needs it.
/// </remarks>
/// <param name="preferences">The preference store.</param>
/// <param name="keys">The configuration keys to expose, such as <c>TutorClientConfig:TenantId</c>.</param>
public sealed class PreferencesConfigurationProvider(IPreferences preferences, IReadOnlyList<string> keys)
    : ConfigurationProvider, IConfigurationSource
{
    /// <inheritdoc/>
    public IConfigurationProvider Build(IConfigurationBuilder builder) => this;

    /// <inheritdoc/>
    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            var value = preferences.Get<string?>(key, null);
            if (!string.IsNullOrWhiteSpace(value))
                data[key] = value;
        }

        Data = data;
    }

    /// <summary>Reloads the values and notifies options monitors of the change.</summary>
    public void Reload()
    {
        Load();
        OnReload();
    }
}
