using CasCap.Common.Authentication;
using Microsoft.AspNetCore.Authentication;

namespace CasCap;

public static partial class AppHost
{
    private static void AddWebApi(WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks();
        builder.Services.AddCasCapConfiguration<ApiAuthConfig>();
        builder.Services.AddAuthentication(BasicAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthenticationHandler.SchemeName, null);
        builder.Services.AddAuthorization();
    }
}
