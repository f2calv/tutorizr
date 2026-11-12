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
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell(
            _serviceProvider.GetRequiredService<ConversationPage>(),
            _serviceProvider.GetService<SettingsPage>()));
}
