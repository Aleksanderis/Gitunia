using Gitunia.App;
using Gitunia.App.Status;
using Gitunia.Core.Status;
using Gitunia.Tui.App.Creation;

namespace Gitunia.Tui.App.Screen;

/// <summary>The message of the status bar: a running remote transfer, the notice of the last action or the latest status.</summary>
/// <param name="host">Holds the running transfer of the active tab.</param>
/// <param name="statuses">The latest status message.</param>
/// <param name="creation">A running clone and its progress.</param>
/// <param name="notices">The notices of the last workspace and pane actions.</param>
public sealed class StatusMessages(
    Dispatcher host,
    StatusReporter statuses,
    CreationInput creation,
    ActionNotices notices)
{
    /// <summary>The text to show now.</summary>
    public string Text() => FirstNotEmpty(notices.Text(), RemoteProgress(), statuses.Current?.Text ?? string.Empty);

    /// <summary>The severity of the latest status; null while a notice or a transfer owns the bar.</summary>
    public StatusSeverity? Severity()
    {
        if (notices.Text().Length > 0 || IsRemoteBusy())
        {
            return null;
        }

        return statuses.Current?.Severity;
    }

    /// <summary>The latest status is new enough to stand out.</summary>
    public bool IsFresh => statuses.IsFresh;

    /// <summary>A clone or a transfer of the active tab runs.</summary>
    public bool IsRemoteBusy() => creation.IsCloning || host.Workspace.ActiveTab?.Sync is not null;

    private string RemoteProgress() =>
        FirstNotEmpty(host.Workspace.ActiveTab?.Sync?.ProgressLine ?? string.Empty, creation.ProgressLine);

    private static string FirstNotEmpty(params string[] texts) =>
        texts.FirstOrDefault(text => text.Length > 0) ?? string.Empty;
}
