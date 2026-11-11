namespace CasCap;

/// <summary>Provides the application navigation shell.</summary>
public sealed partial class AppShell : Shell
{
    /// <summary>Initializes a new instance of the <see cref="AppShell"/> class.</summary>
    /// <param name="page">The constructor-injected conversation page.</param>
    /// <param name="settingsPage">The on-device settings page, on platforms that have one.</param>
    public AppShell(ConversationPage page, SettingsPage? settingsPage = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        InitializeComponent();
        Items.Add(new ShellContent { Content = page, Route = "conversation", Title = "Tutor" });
        if (settingsPage is not null)
            Items.Add(new ShellContent { Content = settingsPage, Route = "settings", Title = "Settings" });
    }
}
