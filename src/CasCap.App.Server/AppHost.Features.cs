using ModelContextProtocol.AspNetCore;

namespace CasCap;

public static partial class AppHost
{
    private static void AddFeatures(WebApplicationBuilder builder, HashSet<string> enabledFeatures)
    {
        var commsEnabled = enabledFeatures.Contains(FeatureNames.Comms);
        if (commsEnabled)
        {
            builder.Services.AddComms(builder.Configuration);
            builder.Services.AddCommsAgent().AddAgentRuntimeAzureAuthentication(builder.Configuration);
        }

        if (enabledFeatures.Contains(FeatureNames.Mcp))
        {
            builder.Services.AddCasCapConfiguration<TutorConfig>();
            builder.Services.AddSingleton<ILearnerStore, RedisLearnerStore>();

            // Only explicitly listed tool types are exposed; stateless sessions need no affinity.
            var mcpBuilder = builder.Services.AddMcpServer()
                .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
                .WithTools<TutorMcpQueryService>();

            // The poll tools post to the chat group, so they exist only where Comms owns that group.
            if (commsEnabled)
                mcpBuilder.WithTools<MessagingMcpQueryService>();
        }

        builder.Services.AddFeatureFlagService(enabledFeatures, addApplicationMetadataService: true);
    }
}
