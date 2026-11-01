using Gitunia.Core.Workspaces.Panes;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views;

/// <summary>Finds the list a pane draws its rows in.</summary>
public interface IPaneLists
{
    /// <summary>The list of a pane.</summary>
    ListView ListOf(PaneKind pane);
}
