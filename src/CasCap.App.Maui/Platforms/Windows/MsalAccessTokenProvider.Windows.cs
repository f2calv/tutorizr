using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Maui.Storage;

namespace CasCap.Services;

public sealed partial class MsalAccessTokenProvider
{
    private const string CacheFileName = "msal.cache";

    private static partial string RedirectUri => "http://localhost";

    private static async partial Task RegisterPersistentCacheAsync(ITokenCache cache)
    {
        var properties = new StorageCreationPropertiesBuilder(CacheFileName, FileSystem.AppDataDirectory).Build();
        (await MsalCacheHelper.CreateAsync(properties)).RegisterCache(cache);
    }

    private static partial AcquireTokenInteractiveParameterBuilder WithParent(AcquireTokenInteractiveParameterBuilder builder) =>
        builder;
}
