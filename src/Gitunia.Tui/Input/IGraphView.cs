using Gitunia.App;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.Input;

/// <summary>What a graph prompt needs from the shell, so the prompts run (and are tested) without Terminal.Gui.</summary>
public interface IGraphView : IActiveSessions
{
    /// <summary>Where a prompt switches the input mode (Insert while it is open).</summary>
    Dispatcher Dispatcher { get; }

    /// <summary>The selected commit row, -1 when nothing is selected.</summary>
    int SelectedRow();

    /// <summary>Selects a commit row and scrolls it into view.</summary>
    /// <param name="row">The commit row.</param>
    void SelectRow(int row);

    /// <summary>Redraws the graph and the status bar.</summary>
    void Redraw();

    /// <summary>The refs of the active tab, none when there is no tab.</summary>
    IEnumerable<GitRef> ActiveRefs();

    /// <summary>The location of an open tab, null when the tab is gone.</summary>
    /// <param name="tabId">The tab.</param>
    RepositoryLocation? LocationOf(TabId tabId);
}
