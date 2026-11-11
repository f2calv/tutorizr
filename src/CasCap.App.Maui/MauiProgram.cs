using CasCap.AgentRuntime.Client.Abstractions;

namespace CasCap;

/// <summary>Configures and creates the MAUI application host.</summary>
public static partial class MauiProgram
{
    /// <summary>Creates the configured MAUI application.</summary>
    /// <returns>The configured application host.</returns>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<TutorApp>();
        AddPlatformServices(builder);

        builder.Services.AddOptionsWithValidateOnStart<TutorClientConfig>()
            .Bind(builder.Configuration.GetSection(TutorClientConfig.ConfigurationSectionName))
            .ValidateDataAnnotations();
        builder.Services.AddSingleton<MsalAccessTokenProvider>();
        builder.Services.AddTransient<BearerTokenHandler>();
        builder.Services.AddAgentRuntimeClient().AddHttpMessageHandler<BearerTokenHandler>();
        builder.Services.AddTransient<Func<IAgentRuntimeClient>>(services => services.GetRequiredService<IAgentRuntimeClient>);
        builder.Services.AddTransient<TutorCallService>();
        builder.Services.AddTransient<ConversationPage>();

#if DEBUG
        builder.Services.AddHybridWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    /// <summary>Adds the current platform's configuration sources and services.</summary>
    /// <param name="builder">The application builder.</param>
    static partial void AddPlatformServices(MauiAppBuilder builder);
}
