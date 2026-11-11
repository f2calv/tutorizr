using Microsoft.Extensions.Configuration;

namespace CasCap;

/// <summary>Configures and creates the MAUI application host.</summary>
public static class MauiProgram
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

        builder.Services.AddOptionsWithValidateOnStart<TutorClientConfig>()
            .Bind(builder.Configuration.GetSection(TutorClientConfig.ConfigurationSectionName))
            .ValidateDataAnnotations();
        builder.Services.AddSingleton<MsalAccessTokenProvider>();
        builder.Services.AddTransient<BearerTokenHandler>();
        builder.Services.AddAgentRuntimeClient().AddHttpMessageHandler<BearerTokenHandler>();
        builder.Services.AddTransient<TutorCallService>();
        builder.Services.AddTransient<ConversationPage>();

#if DEBUG
        builder.Services.AddHybridWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
