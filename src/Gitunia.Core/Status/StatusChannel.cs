namespace Gitunia.Core.Status;

/// <summary>
/// The window's status-message channel: anything that fails or finishes in the background reports through
/// <see cref="Post"/>, and the status bar shows <see cref="Log"/>. Not thread-safe by design: background code posts
/// through the UI-thread dispatcher it already uses for results.
/// </summary>
public sealed class StatusChannel
{
    public StatusLog Log { get; private set; } = StatusLog.Empty;

    /// <summary>Raised after a message was posted or dismissed.</summary>
    public event Action? Changed;

    public void Post(string text, StatusSeverity severity = StatusSeverity.Info)
    {
        if (Log.Latest is { } latest && latest.Text == text && latest.Severity == severity)
        {
            // The same failure again (a repeated refresh) would only fill the history with copies.
            return;
        }

        Log = Log.Post(text, severity);
        Changed?.Invoke();
    }

    public void Error(string text) => Post(text, StatusSeverity.Error);

    public void Warning(string text) => Post(text, StatusSeverity.Warning);

    /// <summary>Hides the current message. Returns false when none was showing.</summary>
    public bool Dismiss()
    {
        if (Log.Latest is null)
        {
            return false;
        }

        Log = Log.Dismiss();
        Changed?.Invoke();
        return true;
    }
}
