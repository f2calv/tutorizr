using Microsoft.Extensions.Configuration;

namespace CasCap;

public static partial class MauiProgram
{
    private const string EnvironmentVariablePrefix = "tutorizr__";

    static partial void AddPlatformServices(MauiAppBuilder builder)
    {
        builder.Configuration.AddUserSecrets<TutorApp>(optional: true);
        builder.Configuration.AddEnvironmentVariables(EnvironmentVariablePrefix);
    }
}
