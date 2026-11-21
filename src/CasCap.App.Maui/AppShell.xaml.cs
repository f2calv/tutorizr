namespace CasCap;

/// <summary>Provides the application navigation shell.</summary>
public sealed partial class AppShell : Shell
{
    /// <summary>Initializes a new instance of the <see cref="AppShell"/> class.</summary>
    /// <param name="page">The constructor-injected conversation page.</param>
    /// <param name="settingsPage">The on-device settings page.</param>
    /// <param name="technicalLogPage">The local call diagnostics page.</param>
    public AppShell(ConversationPage page, SettingsPage settingsPage, TechnicalLogPage technicalLogPage)
    {
        ArgumentNullException.ThrowIfNull(page);

        InitializeComponent();
        var tabs = new TabBar();
        tabs.Items.Add(new ShellContent { Content = page, Route = "conversation", Title = "Tutor" });
        tabs.Items.Add(new ShellContent { Content = settingsPage, Route = "settings", Title = "Settings" });
        tabs.Items.Add(new ShellContent { Content = technicalLogPage, Route = "technical-log", Title = "Technical Log" });
        Items.Add(tabs);
    }
}
