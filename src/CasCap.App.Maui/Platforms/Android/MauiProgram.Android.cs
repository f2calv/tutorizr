using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Storage;

namespace CasCap;

public static partial class MauiProgram
{
    static partial void AddPlatformServices(MauiAppBuilder builder)
    {
        // A sideloaded app has no user secrets; the learner enters the settings on the device.
        var settings = new PreferencesConfigurationProvider(Preferences.Default, SettingsPage.Keys);
        ((IConfigurationBuilder)builder.Configuration).Add(settings);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(Preferences.Default);
        builder.Services.AddTransient<SettingsPage>();
    }
}
