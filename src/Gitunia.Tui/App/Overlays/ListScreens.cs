using Gitunia.Tui.App.FileHistory;
using Gitunia.Tui.App.Reflog;

namespace Gitunia.Tui.App.Overlays;

/// <summary>The list screens that open over the main view: the reflog and the file history.</summary>
/// <param name="Reflog">The reflog list.</param>
/// <param name="FileHistory">The file history.</param>
public sealed record ListScreens(ReflogInput Reflog, FileHistoryInput FileHistory)
{
    /// <summary>One of the list screens is open.</summary>
    public bool IsOpen => Reflog.IsOpen || FileHistory.IsOpen;
}
