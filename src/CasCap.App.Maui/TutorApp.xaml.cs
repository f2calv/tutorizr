namespace CasCap;

/// <summary>Defines the application and creates its root window.</summary>
public sealed partial class TutorApp : Application
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Initializes a new instance of the <see cref="TutorApp"/> class.</summary>
    /// <param name="serviceProvider">The application service provider used to resolve each window's page.</param>
    public TutorApp(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var conversation = _serviceProvider.GetRequiredService<ConversationPage>();
        var window = new Window(new AppShell(conversation, _serviceProvider.GetRequiredService<SettingsPage>(),
            _serviceProvider.GetRequiredService<TechnicalLogPage>()));
        window.Activated += (_, _) => conversation.SetForeground(true);
        window.Deactivated += (_, _) => conversation.SetForeground(false);
        window.Stopped += async (_, _) => await conversation.SuspendAsync();
        window.Destroying += async (_, _) => await conversation.SuspendAsync();
        return window;
    }
}
