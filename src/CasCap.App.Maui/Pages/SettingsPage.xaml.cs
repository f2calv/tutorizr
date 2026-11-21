using Microsoft.Identity.Client;
using Microsoft.Maui.Accessibility;
using Microsoft.Maui.Storage;
using System.Net;
using System.Text.Json;

namespace CasCap.Pages;

/// <summary>Edits voice preferences and, on Android, the tutor connection settings.</summary>
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
    private readonly PreferencesConfigurationProvider? _settings;
    private readonly VoiceCallPreferences _voicePreferences;
    private readonly Func<VoiceCapabilityClient> _capabilityClientFactory;

    /// <summary>Initializes a new instance of the <see cref="SettingsPage"/> class.</summary>
    /// <param name="preferences">The device preference store.</param>
    /// <param name="voicePreferences">The current app session's voice preferences.</param>
    /// <param name="capabilityClientFactory">Creates the runtime client when the user requests a check.</param>
    /// <param name="settings">The configuration provider, when the platform supports editing connection settings.</param>
    public SettingsPage(
        IPreferences preferences,
        VoiceCallPreferences voicePreferences,
        Func<VoiceCapabilityClient> capabilityClientFactory,
        PreferencesConfigurationProvider? settings = null)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(voicePreferences);
        ArgumentNullException.ThrowIfNull(capabilityClientFactory);

        InitializeComponent();
        _preferences = preferences;
        _settings = settings;
        _voicePreferences = voicePreferences;
        _capabilityClientFactory = capabilityClientFactory;
        ConnectionSettings.IsVisible = settings is not null;
        WindowsSettingsHint.IsVisible = settings is null;
    }

    /// <inheritdoc/>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        RememberConversationSwitch.IsToggled = _voicePreferences.RetainNewCalls;
        if (_settings is null)
            return;

        TenantIdEntry.Text = _preferences.Get(TenantIdKey, string.Empty);
        ClientIdEntry.Text = _preferences.Get(ClientIdKey, string.Empty);
        ApiScopeEntry.Text = _preferences.Get(ApiScopeKey, string.Empty);
        AgentNameEntry.Text = _preferences.Get(AgentNameKey, string.Empty);
        BaseAddressEntry.Text = _preferences.Get(BaseAddressKey, string.Empty);
    }

    private void OnRetentionToggled(object? sender, ToggledEventArgs e) =>
        _voicePreferences.RetainNewCalls = e.Value;

    private async void OnCheckCapabilitiesClicked(object? sender, EventArgs e)
    {
        CheckCapabilitiesButton.IsEnabled = false;
        SaveSettingsButton.IsEnabled = false;
        CapabilityStatusLabel.Text = "Checking tutor capabilities…";
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            CapabilityStatusLabel.Text = await ReadCapabilitiesAsync(timeout.Token);
        }
        finally
        {
            CheckCapabilitiesButton.IsEnabled = true;
            SaveSettingsButton.IsEnabled = true;
        }
        SemanticScreenReader.Default.Announce(CapabilityStatusLabel.Text);
    }

    private async Task<string> ReadCapabilitiesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var capabilities = await _capabilityClientFactory().GetAsync(cancellationToken);
            return capabilities is null
                ? "No capability information. Check the tutor name and whether the runtime supports this check."
                : $"Voice: {(capabilities.VoiceEnabled ? "configured" : "not configured")}\n"
                    + $"Knowledge tools for voice: {(capabilities.KnowledgeToolsEnabled ? "enabled" : "disabled")}\n"
                    + $"Retention policy: {(capabilities.RetentionAllowed ? "allowed by the definition" : "not allowed")}\n"
                    + "This does not check provider health, permissions for knowledge search, or an existing conversation.";
        }
        catch (OptionsValidationException)
        {
            return "Tutor connection settings are incomplete.";
        }
        catch (MsalException)
        {
            return "Sign-in failed or was cancelled. Tutor capabilities are unknown.";
        }
        catch (OperationCanceledException)
        {
            return "The capability check timed out or was cancelled.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return "Your account is not allowed to check this tutor's voice capabilities.";
        }
        catch (HttpRequestException)
        {
            return "Could not read tutor capabilities from the runtime.";
        }
        catch (JsonException)
        {
            return "The runtime did not return valid capability information.";
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_settings is null)
            return;

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
            errors.Add("Enter the runtime API scopes and the tutor agent name.");
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
        CapabilityStatusLabel.Text = "Not checked. Reopen the app before checking if you already connected to the runtime.";
        // Sign-in and runtime settings may already have been read by a capability check or call.
        const string saved = "If you already checked capabilities or started a call, reopen the app to use the new settings.";
        ShowStatus($"Saved. {saved}");
        await DisplayAlertAsync("Settings saved", saved, "OK");
    }

    private void ShowStatus(string text)
    {
        StatusLabel.Text = text;
        SemanticScreenReader.Default.Announce(text);
    }
}
