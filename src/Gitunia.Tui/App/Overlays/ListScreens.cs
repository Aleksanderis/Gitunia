using Gitunia.Tui.App.Blame;
using Gitunia.Tui.App.FileHistory;
using Gitunia.Tui.App.Reflog;

namespace Gitunia.Tui.App.Overlays;

/// <summary>The list-style screens that cover the panes: the reflog list, the blame view and the file history.</summary>
/// <param name="Reflog">The reflog list.</param>
/// <param name="Blame">The blame view.</param>
/// <param name="FileHistory">The file history.</param>
public sealed record ListScreens(ReflogInput Reflog, BlameInput Blame, FileHistoryInput FileHistory);
