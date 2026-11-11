using Microsoft.Identity.Client;
using Microsoft.Maui.ApplicationModel;

namespace CasCap.Services;

/// <summary>
/// Signs the learner in through the tutor's public client registration and returns delegated Agent Runtime tokens.
/// </summary>
/// <remarks>
/// Uses the system browser and no authentication broker; each platform supplies its redirect URI. MSAL persists the
/// cache in app-private storage on Android. TODO: persist the Windows cache (for example with
/// Microsoft.Identity.Client.Extensions.Msal) so a restart does not prompt again; kept in memory for the proof of concept.
/// </remarks>
public sealed partial class MsalAccessTokenProvider
{
    private readonly IPublicClientApplication _application;
    private readonly string[] _scopes;
    private readonly SemaphoreSlim _interactiveLock = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="MsalAccessTokenProvider"/> class.</summary>
    /// <param name="config">The validated tutor client settings.</param>
    public MsalAccessTokenProvider(IOptions<TutorClientConfig> config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _application = PublicClientApplicationBuilder.Create(config.Value.ClientId)
            .WithTenantId(config.Value.TenantId)
            .WithRedirectUri(RedirectUri)
            .Build();
        _scopes = [config.Value.ApiScope!];
    }

    /// <summary>Returns a cached or refreshed token, prompting in the system browser only when sign-in is required.</summary>
    /// <param name="cancellationToken">Cancels the token request.</param>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (await TryAcquireSilentAsync(cancellationToken) is { } token)
            return token;

        await _interactiveLock.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have completed sign-in while this one waited.
            return await TryAcquireSilentAsync(cancellationToken)
                ?? (await MainThread.InvokeOnMainThreadAsync(() => WithParent(_application.AcquireTokenInteractive(_scopes))
                    .WithUseEmbeddedWebView(false)
                    .ExecuteAsync(cancellationToken))).AccessToken;
        }
        finally
        {
            _interactiveLock.Release();
        }
    }

    private async Task<string?> TryAcquireSilentAsync(CancellationToken cancellationToken)
    {
        var account = (await _application.GetAccountsAsync()).FirstOrDefault();
        if (account is null)
            return null;
        try
        {
            return (await _application.AcquireTokenSilent(_scopes, account).ExecuteAsync(cancellationToken)).AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            return null;
        }
    }

    /// <summary>Gets the redirect URI registered for this platform on the public client.</summary>
    private static partial string RedirectUri { get; }

    /// <summary>Attaches the platform's parent activity or window to an interactive sign-in.</summary>
    private static partial AcquireTokenInteractiveParameterBuilder WithParent(AcquireTokenInteractiveParameterBuilder builder);
}
