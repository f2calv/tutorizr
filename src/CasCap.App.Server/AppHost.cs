using Serilog;
using System.Reflection;

namespace CasCap;

/// <summary>Builds and runs the tutorizr host.</summary>
public static partial class AppHost
{
    /// <summary>Bootstraps and runs the application.</summary>
    /// <param name="args">Command-line arguments forwarded from the entry point.</param>
    /// <param name="entryAssembly">Entry assembly used for configuration and user-secrets resolution.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, Assembly entryAssembly)
    {
        SerilogExtensions.GetBootstrapLogger();

        try
        {
            // Host builder
            var builder = WebApplication.CreateBuilder(args);

            // Configuration
            var (appConfig, enabledFeatures, applicationMetadata) = builder.InitializeConfiguration(entryAssembly);

            // Logging
            var logger = SerilogWebApplicationBuilderExtensions.InitializeSerilog(builder);

            // Infrastructure
            builder.Services.AddSingleton(TimeProvider.System);
            var connectionMultiplexer = builder.Services.AddCasCapCaching(builder.Configuration)
                ?? throw new InvalidOperationException("CasCap:CachingConfig:RemoteCacheConnectionString must configure Redis.");

            // Observability
            builder.InitializeOpenTelemetry(appConfig, applicationMetadata, connectionMultiplexer);

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("{ClassName} {AppName} running with features {@Features}",
                    nameof(AppHost), AppDomain.CurrentDomain.FriendlyName, enabledFeatures);

            // Feature registration
            AddFeatures(builder, enabledFeatures);

            // Web API registration
            AddWebApi(builder);

            // Build
            var app = builder.Build();

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("{ClassName} starting", nameof(AppHost));

            // Endpoint mapping
            MapEndpoints(app, appConfig, enabledFeatures);

            // Run
            await app.RunAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not TaskCanceledException)
        {
            Log.Fatal(exception, "{AppName} terminated unexpectedly", AppDomain.CurrentDomain.FriendlyName);
            throw new InvalidOperationException("Application host terminated unexpectedly.", exception);
        }
        finally
        {
            Log.Information("Stopped {AppName}", AppDomain.CurrentDomain.FriendlyName);
            await Log.CloseAndFlushAsync();
        }

        return 0;
    }
}
