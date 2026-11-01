using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Panes that share one list, with an optional list of their own for the staged files.</summary>
/// <param name="list">The list of every pane without a list of its own.</param>
/// <param name="staged">The list of the staged files, null to use <paramref name="list"/>.</param>
public sealed class FakePaneLists(ListView list, ListView? staged = null) : IPaneLists
{
    public ListView ListOf(PaneKind pane) => pane == PaneKind.WorkingStaged && staged is not null ? staged : list;
}
