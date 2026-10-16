using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Net.Http.Headers;
using Serilog;

namespace CasCap;

public static partial class AppHost
{
    private static void MapEndpoints(WebApplication app, AppConfig appConfig, HashSet<string> enabledFeatures)
    {
        app.UseAuthentication();
        app.UseAuthorization();

        if (enabledFeatures.Contains(FeatureNames.Mcp))
        {
            // Editor and runtime clients send no Origin; a browser always does, so reject it before any
            // tool runs (the SDK does not validate Origin).
            app.MapGroup(appConfig.McpUrl)
                .AddEndpointFilter(async (context, next) =>
                    context.HttpContext.Request.Headers.ContainsKey(HeaderNames.Origin)
                        ? Results.StatusCode(StatusCodes.Status403Forbidden)
                        : await next(context))
                .RequireAuthorization()
                .MapMcp();
        }

        app.MapHealthChecks("/healthz").AllowAnonymous();
        foreach (var probeType in Enum.GetValues<KubernetesProbeTypes>())
        {
            if (probeType is KubernetesProbeTypes.None)
                continue;

            var tag = probeType.GetDescription();
            app.MapHealthChecks($"/healthz/{tag}", new HealthCheckOptions
            {
                Predicate = healthCheck => healthCheck.Tags.Contains(tag)
            }).AllowAnonymous();
        }

        app.UseCasCapRequestLogging();
    }
}
