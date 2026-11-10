namespace CasCap;

/// <summary>Provides the application navigation shell.</summary>
public sealed partial class AppShell : Shell
{
    /// <summary>Initializes a new instance of the <see cref="AppShell"/> class.</summary>
    /// <param name="page">The constructor-injected conversation page.</param>
    public AppShell(ConversationPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        InitializeComponent();
        Items.Add(new ShellContent { Content = page, Route = "conversation", Title = "Tutor" });
    }
}
