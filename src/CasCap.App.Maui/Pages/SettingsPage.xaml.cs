using Microsoft.Maui.Accessibility;
using Microsoft.Maui.Storage;

namespace CasCap.Pages;

/// <summary>Edits the tutor settings entered on a device that has no user secrets.</summary>
public sealed partial class SettingsPage : ContentPage
{
    /// <summary>Preference keys, named after the configuration keys they supply.</summary>
    public static readonly IReadOnlyList<string> Keys = new[]
    {
        TenantIdKey, ClientIdKey, ApiScopeKey, AgentNameKey, BaseAddressKey,
    };

    private const string TenantIdKey = "TutorClientConfig:TenantId";
    private const string ClientIdKey = "TutorClientConfig:ClientId";
    private const string ApiScopeKey = "TutorClientConfig:ApiScope";
    private const string AgentNameKey = "TutorClientConfig:AgentName";
    private const string BaseAddressKey = "CasCap:AgentRuntimeClientOptions:BaseAddress";

    private readonly IPreferences _preferences;
    private readonly PreferencesConfigurationProvider _settings;

    /// <summary>Initializes a new instance of the <see cref="SettingsPage"/> class.</summary>
    /// <param name="preferences">The device preference store.</param>
    /// <param name="settings">The configuration provider backed by the same preferences.</param>
    public SettingsPage(IPreferences preferences, PreferencesConfigurationProvider settings)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(settings);

        InitializeComponent();
        _preferences = preferences;
        _settings = settings;
    }

    /// <inheritdoc/>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        TenantIdEntry.Text = _preferences.Get(TenantIdKey, string.Empty);
        ClientIdEntry.Text = _preferences.Get(ClientIdKey, string.Empty);
        ApiScopeEntry.Text = _preferences.Get(ApiScopeKey, string.Empty);
        AgentNameEntry.Text = _preferences.Get(AgentNameKey, string.Empty);
        BaseAddressEntry.Text = _preferences.Get(BaseAddressKey, string.Empty);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var values = new Dictionary<string, string>
        {
            [TenantIdKey] = (TenantIdEntry.Text ?? string.Empty).Trim(),
            [ClientIdKey] = (ClientIdEntry.Text ?? string.Empty).Trim(),
            [ApiScopeKey] = (ApiScopeEntry.Text ?? string.Empty).Trim(),
            [AgentNameKey] = (AgentNameEntry.Text ?? string.Empty).Trim(),
            [BaseAddressKey] = (BaseAddressEntry.Text ?? string.Empty).Trim(),
        };
        var errors = new List<string>();
        if (!Guid.TryParse(values[TenantIdKey], out _))
            errors.Add("The directory ID must be a GUID.");
        if (!Guid.TryParse(values[ClientIdKey], out _))
            errors.Add("The application ID must be a GUID.");
        if (values[ApiScopeKey].Length == 0 || values[AgentNameKey].Length == 0)
            errors.Add("Enter the runtime API scope and the tutor agent name.");
        if (!Uri.TryCreate(values[BaseAddressKey], UriKind.Absolute, out var address) || address.Scheme != Uri.UriSchemeHttps)
            errors.Add("The runtime address must be an https:// URL.");
        if (errors.Count > 0)
        {
            ShowStatus(string.Join(" ", errors));
            await DisplayAlertAsync("Settings not saved", string.Join(Environment.NewLine, errors), "OK");
            return;
        }

        foreach (var (key, value) in values)
            _preferences.Set(key, value);
        _settings.Reload();
        // Sign-in and the runtime client read their settings once, when the first call starts.
        const string saved = "If you already started a call, close and reopen the app to use the new settings.";
        ShowStatus($"Saved. {saved}");
        await DisplayAlertAsync("Settings saved", saved, "OK");
    }

    private void ShowStatus(string text)
    {
        StatusLabel.Text = text;
        SemanticScreenReader.Default.Announce(text);
    }
}
