using Microsoft.Identity.Client;

namespace CasCap.Services;

public sealed partial class MsalAccessTokenProvider
{
    private static partial string RedirectUri => "http://localhost";

    private static partial AcquireTokenInteractiveParameterBuilder WithParent(AcquireTokenInteractiveParameterBuilder builder) =>
        builder;
}
