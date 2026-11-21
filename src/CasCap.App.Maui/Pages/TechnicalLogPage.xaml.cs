using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace CasCap.Pages;

/// <summary>Shows recent call setup diagnostics without conversational data.</summary>
public sealed partial class TechnicalLogPage : ContentPage
{
    private readonly CallDiagnosticLog _log;

    /// <summary>Initializes the page with the current app session's call diagnostics.</summary>
    public TechnicalLogPage(CallDiagnosticLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _log.Changed += OnLogChanged;
        Refresh();
    }

    /// <inheritdoc/>
    protected override void OnDisappearing()
    {
        _log.Changed -= OnLogChanged;
        base.OnDisappearing();
    }

    private void OnLogChanged(object? sender, EventArgs e) => Refresh();

    private void OnClearClicked(object? sender, EventArgs e) => _log.Clear();

    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        try
        {
            await Clipboard.Default.SetTextAsync(LogLabel.Text);
        }
        catch (InvalidOperationException)
        {
            await DisplayAlertAsync("Could not copy", "The clipboard is unavailable.", "OK");
        }
    }

    private void Refresh()
    {
        var text = string.Join(Environment.NewLine + Environment.NewLine, _log.GetEntries());
        var hasEntries = text.Length > 0;
        LogLabel.Text = hasEntries ? text : "No call setup results yet.";
        CopyLogButton.IsEnabled = hasEntries;
        ClearLogButton.IsEnabled = hasEntries;
    }
}
