using Microsoft.Identity.Client;
using Microsoft.Maui.ApplicationModel;

namespace CasCap.Services;

public sealed partial class MsalAccessTokenProvider
{
    /// <summary>The redirect URI scheme; it is the application ID, so it needs no per-registration value.</summary>
    public const string RedirectScheme = "io.github.f2calv.tutorizr";

    private static partial string RedirectUri => RedirectScheme + "://auth";

    private static partial AcquireTokenInteractiveParameterBuilder WithParent(AcquireTokenInteractiveParameterBuilder builder) =>
        builder.WithParentActivityOrWindow(Platform.CurrentActivity
            ?? throw new InvalidOperationException("Sign-in needs the app in the foreground."));
}
