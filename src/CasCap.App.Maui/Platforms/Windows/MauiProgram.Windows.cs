namespace CasCap;

public static partial class MauiProgram
{
    static partial void AddPlatformServices(MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<IRealtimeAudioInput, WasapiMicrophone>();
        builder.Services.AddSingleton<IRealtimeAudioOutput, WaveOutSpeaker>();
    }
}
