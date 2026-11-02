using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>The graph and the detail list; every other pane gets the graph's.</summary>
/// <param name="graph">The graph list.</param>
/// <param name="detail">The detail list.</param>
public sealed class FakeGraphDetailLists(ListView graph, ListView detail) : IPaneLists
{
    public ListView ListOf(PaneKind pane) => pane == PaneKind.Detail ? detail : graph;
}
