using Gitunia.App;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Shell;

/// <summary>The graph list of the shell as seen by the graph prompts (search, ref jump).</summary>
/// <param name="foundation">The state, the sessions and the widgets.</param>
public sealed class TerminalGraphView(ShellFoundation foundation) : IGraphView
{
    /// <inheritdoc />
    public Dispatcher Dispatcher => foundation.Host;

    /// <inheritdoc />
    public TerminalTabSession? ActiveSession() => foundation.Sessions.ActiveSession();

    /// <inheritdoc />
    public int SelectedRow() =>
        foundation.Panes.Graph.SelectedItem is { } item ? GraphListSource.CommitIndexOf(item) : -1;

    /// <inheritdoc />
    public void SelectRow(int row)
    {
        foundation.Panes.Graph.SelectedItem = row + ScreenLayout.ReservedLeadingRows;
        foundation.Panes.Graph.EnsureSelectedItemVisible();
    }

    /// <inheritdoc />
    public void Redraw()
    {
        foundation.Panes.Graph.SetNeedsDraw();
        foundation.Chrome.RefreshChrome();
    }

    /// <inheritdoc />
    public IEnumerable<GitRef> ActiveRefs() => foundation.Host.Workspace.ActiveTab?.Refs ?? [];

    /// <inheritdoc />
    public RepositoryLocation? LocationOf(TabId tabId) => foundation.Host.Workspace.FindTab(tabId)?.Location;
}
