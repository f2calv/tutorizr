namespace CasCap.WinUI;

/// <summary>Hosts the MAUI application in a WinUI process.</summary>
public sealed partial class App : MauiWinUIApplication
{
    /// <summary>Initializes a new instance of the <see cref="App"/> class.</summary>
    public App()
    {
        // Tutor audio starts when the call connects, after a native button click the web view never sees.
        var browserArguments = "--autoplay-policy=no-user-gesture-required";
#if DEBUG
        browserArguments += " --remote-debugging-port=9222";
#endif
        Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", browserArguments);
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
