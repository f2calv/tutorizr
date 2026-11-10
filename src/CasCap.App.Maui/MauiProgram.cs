using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace CasCap;

/// <summary>Configures and creates the MAUI application host.</summary>
public static partial class MauiProgram
{
    private const string EnvironmentVariablePrefix = "tutorizr__";

    /// <summary>Creates the configured MAUI application.</summary>
    /// <returns>The configured application host.</returns>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<TutorApp>();
        builder.Configuration.AddUserSecrets<TutorApp>(optional: true);
        builder.Configuration.AddEnvironmentVariables(EnvironmentVariablePrefix);

        builder.Services.AddOptionsWithValidateOnStart<RealtimeConfig>()
            .Bind(builder.Configuration.GetSection(RealtimeConfig.ConfigurationSectionName))
            .ValidateDataAnnotations();
        builder.Services.AddSingleton<TokenCredential>(_ => CreateCredential(builder.Configuration["AzureTenantId"]));
        builder.Services.AddSingleton<RealtimeConversation>();
        builder.Services.AddTransient<ConversationPage>();
        AddPlatformServices(builder);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    // TODO: replace with MSAL public-client sign-in when the Android target is added; a single developer workstation
    // signs in through the Azure CLI or Visual Studio, falling back to the system browser.
    private static ChainedTokenCredential CreateCredential(string? tenantId) => new(
        new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenantId }),
        new VisualStudioCredential(new VisualStudioCredentialOptions { TenantId = tenantId }),
        new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions { TenantId = tenantId }));

    /// <summary>Registers the audio devices implemented by the current platform.</summary>
    /// <param name="builder">The application builder.</param>
    static partial void AddPlatformServices(MauiAppBuilder builder);
}
