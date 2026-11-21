using CasCap.AgentRuntime.Client.Abstractions;
using Microsoft.Maui.Storage;

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
        builder.Services.AddTransient<Func<MsalAccessTokenProvider>>(services =>
            services.GetRequiredService<MsalAccessTokenProvider>);
        builder.Services.AddTransient<BearerTokenHandler>();
        builder.Services.AddAgentRuntimeClient()
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddTypedClient<VoiceCapabilityClient>();
        builder.Services.AddTransient<Func<IAgentRuntimeClient>>(services => services.GetRequiredService<IAgentRuntimeClient>);
        builder.Services.AddTransient<Func<VoiceCapabilityClient>>(services => services.GetRequiredService<VoiceCapabilityClient>);
        builder.Services.AddTransient<TutorCallService>();
        builder.Services.AddTransient<ConversationPage>();
        builder.Services.AddSingleton(Preferences.Default);
        builder.Services.AddSingleton<VoiceCallPreferences>();
        builder.Services.AddSingleton<CallDiagnosticLog>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<TechnicalLogPage>();

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
