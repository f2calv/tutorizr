using CasCap.Common.Extensions;
using Microsoft.Extensions.Configuration;

namespace CasCap;

public static partial class MauiProgram
{
    private const string EnvironmentVariablePrefix = "tutorizr__";

    static partial void AddPlatformServices(MauiAppBuilder builder)
    {
#if DEBUG
        builder.Configuration.AddStandardConfiguration(assembly: typeof(TutorApp).Assembly);
#else
        builder.Configuration.AddUserSecrets<TutorApp>(optional: true);
#endif
        builder.Configuration.AddEnvironmentVariables(EnvironmentVariablePrefix);
    }
}
