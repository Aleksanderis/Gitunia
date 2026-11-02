using Gitunia.Tui.App.Blame;
using Gitunia.Tui.App.Reflog;

namespace Gitunia.Tui.App.Overlays;

/// <summary>The list-style screens that cover the panes: the reflog list and the blame view.</summary>
/// <param name="Reflog">The reflog list.</param>
/// <param name="Blame">The blame view.</param>
public sealed record ListScreens(ReflogInput Reflog, BlameInput Blame);
