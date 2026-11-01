using Gitunia.Core.FileTree;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Diff;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Diff.Models;

namespace Gitunia.Core.DiffPane;

/// <summary>A key, a cursor move or a store event of the diff pane of one tab. Dropped when the tab is no longer
/// open.</summary>
public abstract record DiffPaneCommand : AppCommand
{
    private protected DiffPaneCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>
/// The selection or the screen may have changed what the diff should show: load the selected commit's diff when the
/// pane is on screen, show the file under the tree's cursor beside the Files tab, or drop a file of another commit.
/// </summary>
/// <param name="TabId">The tab.</param>
/// <param name="Facts">What the views and stores know.</param>
public sealed record DiffFollowed(TabId TabId, DiffFollowFacts Facts) : DiffPaneCommand(TabId);

/// <summary>Enter on a file or folder of the Changes tab: show its diff, opening the pane where it is not beside the
/// list, and focus it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Facts">What the views and stores know.</param>
public sealed record DiffFileOpened(TabId TabId, DiffOpenFacts Facts) : DiffPaneCommand(TabId);

/// <summary>Enter on a file of the Files tab: show its content in the diff pane's place and focus it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Row">The file row.</param>
/// <param name="TreeCommit">The commit the tree shows, null while the tree is still loading.</param>
public sealed record DiffContentOpened(TabId TabId, FileTreeRow Row, ObjectId? TreeCommit) : DiffPaneCommand(TabId);

/// <summary>The cursor of the detail list moved: the pane beside it shows that file or folder.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Cursor">What the Changes list's cursor is on.</param>
/// <param name="Content">What the Files tab's pane should show for its cursor.</param>
public sealed record DiffCursorMoved(TabId TabId, DiffCursor Cursor, DiffContentTarget Content)
    : DiffPaneCommand(TabId);

/// <summary>The diff store loaded or changed: take the pending file or folder from the loaded diff.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Full">The diff the store holds.</param>
/// <param name="IsLoaded">The diff has loaded (not loading or failed).</param>
public sealed record DiffRefreshed(TabId TabId, DiffDocument Full, bool IsLoaded) : DiffPaneCommand(TabId);

/// <summary>A diff option key (context, whole file, whitespace) in the diff pane.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Action">The key.</param>
/// <param name="Current">The options the store reads with.</param>
/// <param name="CurrentFile">The file under the diff's cursor, shown again after the reload.</param>
public sealed record DiffOptionsRequested(TabId TabId, ActionId Action, DiffOptions Current, string? CurrentFile)
    : DiffPaneCommand(TabId);

/// <summary>Load the whole diff after it was cut short.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="IsCut">The shown diff is loaded and was cut short.</param>
/// <param name="CurrentFile">The file under the diff's cursor, shown again after the reload.</param>
public sealed record DiffLoadAnywayRequested(TabId TabId, bool IsCut, string? CurrentFile) : DiffPaneCommand(TabId);

/// <summary><c>]f</c> and <c>[f</c> picked another file of the commit.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Path">The file.</param>
public sealed record DiffFileChosen(TabId TabId, string Path) : DiffPaneCommand(TabId);

/// <summary><c>Esc</c> in the diff: back to the file list, closing the pane where it is not beside it.</summary>
/// <param name="TabId">The tab.</param>
public sealed record DiffClosed(TabId TabId) : DiffPaneCommand(TabId);

/// <summary>Zoom the focused pane, or unzoom it.</summary>
/// <param name="TabId">The tab.</param>
public sealed record DiffZoomed(TabId TabId) : DiffPaneCommand(TabId);

/// <summary>The tab's stores were rebuilt (reload, eviction): the pane starts over.</summary>
/// <param name="TabId">The tab.</param>
public sealed record DiffPaneReset(TabId TabId) : DiffPaneCommand(TabId);
