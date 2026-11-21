namespace CasCap.Services;

/// <summary>Holds recent, privacy-safe call setup diagnostics in memory on the device.</summary>
public sealed class CallDiagnosticLog
{
    private const int Capacity = 25;
    private readonly Queue<string> _entries = new();

    /// <summary>Occurs when a call setup entry is added or the log is cleared on the UI thread.</summary>
    public event EventHandler? Changed;

    /// <summary>Returns the current entries, newest first.</summary>
    public IEnumerable<string> GetEntries() => _entries.Reverse();

    /// <summary>Records a client-side call setup outcome without exception messages or response bodies.</summary>
    public void RecordSetup(bool resume, bool retain, TimeSpan elapsed, string stage, string outcome, Exception? error = null)
    {
        var mode = resume ? "Resume" : retain ? "New (retained)" : "New (not retained)";
        var details = error switch
        {
            HttpRequestException { StatusCode: { } status } => $" | HTTP {(int)status} ({status})",
            HttpRequestException request => $" | {request.HttpRequestError}",
            not null => $" | {error.GetType().Name}",
            _ => string.Empty,
        };
        if (_entries.Count == Capacity)
            _entries.Dequeue();
        _entries.Enqueue(FormattableString.Invariant(
            $"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | {mode} | {stage} | {elapsed.TotalSeconds:F1}s | {outcome}{details}"));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clears the current app session's diagnostics.</summary>
    public void Clear()
    {
        _entries.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
