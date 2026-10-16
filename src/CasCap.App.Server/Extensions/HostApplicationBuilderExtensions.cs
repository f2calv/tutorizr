using System.Reflection;

namespace CasCap.Extensions;

/// <summary>Configuration bootstrapping for the tutorizr host.</summary>
public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the standard configuration sources and Key Vault secrets, then binds the application,
    /// Basic authentication and feature configuration.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="assembly">Assembly used for user-secrets loading.</param>
    public static (AppConfig appConfig, HashSet<string> enabledFeatures, ApplicationMetadata applicationMetadata) InitializeConfiguration(
        this IHostApplicationBuilder builder, Assembly assembly)
    {
        builder.Configuration.AddStandardConfiguration(builder.Environment.EnvironmentName, assembly);

        var azureAuthConfig = builder.Configuration.GetSection(AzureAuthConfig.ConfigurationSectionName).Get<AzureAuthConfig>();
        if (azureAuthConfig?.IsKeyVaultEnabled is true)
        {
            //Loads this host's Agent Runtime execution identity and excludes every other application's.
            builder.Configuration.AddKeyVaultConfiguration(
                azureAuthConfig.KeyVaultUri,
                azureAuthConfig.TokenCredential,
                new PrefixKeyVaultSecretManager(
                    "AgentRuntime--Tutorizr--Exec",
                    AgentRuntimeAzureAuthConfig.ConfigurationSectionName,
                    "AgentRuntime"));
        }
        //The Azure-backed voice clients read their credential from AzureAuthConfig.
        builder.Services.AddCasCapConfiguration<AzureAuthConfig>();

        var appConfig = builder.Services.AddAndGetCasCapConfiguration<AppConfig>(builder.Configuration);
        var enabledFeatures = builder.Services.AddAndGetCasCapConfiguration<FeatureConfig>(builder.Configuration).GetEnabledFeatures();

        var applicationMetadata = new ApplicationMetadata(assembly);
        builder.Services.AddSingleton(applicationMetadata);

        return (appConfig, enabledFeatures, applicationMetadata);
    }
}
