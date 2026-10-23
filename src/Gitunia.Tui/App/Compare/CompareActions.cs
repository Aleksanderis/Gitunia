using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading.Commits;

namespace Gitunia.Tui.App.Compare;

/// <summary>
/// Compares two commits of the graph (#535, #636, ux.md "Comparing two commits"). <c>&lt;space&gt;=</c> marks the
/// selected commit; while a mark is set, every other commit under the cursor is compared with it (<see cref="Follow"/>:
/// <c>git diff older newer</c>, older and newer by graph order, in the Changes tab and the diff pane). On another
/// commit the key moves the mark, on the marked commit it clears it. <c>Esc</c> in the graph clears the mark and the
/// comparison. The mark is part of the tab's state, so each tab has its own. Runs without a terminal.
/// </summary>
/// <param name="host">The workspace, which holds the mark of each tab.</param>
/// <param name="activeSession">The active tab's session, null when there is none.</param>
/// <param name="selectedCommit">The commit under the graph cursor, null when none.</param>
public sealed class CompareActions(WorkspaceHost host, Func<TabSession?> activeSession, Func<ObjectId?> selectedCommit)
{
    private const int HashLength = 7;

    private readonly Dictionary<TabId, TabFollow> _follows = [];

    /// <summary>A short notice for the status bar from the last handled action, empty when none.</summary>
    public string Notice { get; private set; } = string.Empty;

    /// <summary>Handles the compare key and the Esc that clears the mark; false for every other action.</summary>
    public bool TryExecute(ActionId action)
    {
        Notice = string.Empty;
        if (action == ActionIds.GraphCompare)
        {
            Compare();
            return true;
        }

        if (action == ActionIds.Cancel
            && host.Workspace.ActiveTab is { View: { CompareMark: not null, FocusedPane: PaneKind.Graph } } tab)
        {
            Clear(tab);
            Notice = "Comparison mark cleared";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Compares the commit under the cursor with the marked one once its own details have loaded (#636). Called when
    /// the details store of the active tab changes; does nothing without a mark, on the marked commit, or while the
    /// comparison for this commit is already asked for. The own details load goes through the store's debounce, so
    /// holding a movement key starts no process per row.
    /// </summary>
    public void Follow()
    {
        if (host.Workspace.ActiveTab is not { } tab || activeSession() is not { } session)
        {
            return;
        }

        var current = session.Details.Current;
        var follow = FollowOf(tab.Id);
        if (current.Status != CommitDetailsStatus.Loaded || current.Commit != follow.Followed)
        {
            // A reload (after a failure, Retry) starts the comparison again.
            follow.Followed = null;
        }

        if (tab.View.CompareMark is not { } mark
            || current is not { Status: CommitDetailsStatus.Loaded, Commit: { } selected, Details.IsComparison: false }
            || selected == mark
            || follow.Followed == selected)
        {
            return;
        }

        var markIndex = session.Graph.IndexOf(mark);
        var selectedIndex = session.Graph.IndexOf(selected);
        if (markIndex < 0 || selectedIndex < 0)
        {
            return;
        }

        // The graph lists the newest commit first, so the larger index is the older commit.
        var older = markIndex > selectedIndex ? mark : selected;
        var newer = markIndex > selectedIndex ? selected : mark;
        if (!session.Details.Compare(older, newer))
        {
            return;
        }

        follow.Followed = selected;
        if (!follow.ShownChanges)
        {
            follow.ShownChanges = true;
            host.Dispatch(new TabCommand(tab.Id, new SetBottomTabCommand(BottomTab.Changes)));
        }
    }

    private void Compare()
    {
        if (host.Workspace.ActiveTab is not { } tab || activeSession() is not { } session)
        {
            return;
        }

        if (selectedCommit() is not { } selected)
        {
            Notice = "No commit selected";
            return;
        }

        if (tab.View.CompareMark is not { } mark)
        {
            FollowOf(tab.Id).ShownChanges = false;
            host.Dispatch(new TabCommand(tab.Id, new SetCompareMarkCommand(selected)));
            Notice = $"Marked {Short(selected)}: move to another commit to compare it (Esc ends)";
            return;
        }

        if (mark == selected)
        {
            Clear(tab);
            Notice = "Comparison mark cleared";
            return;
        }

        // The mark moves here, so this commit's own changes show and the next move compares with it.
        FollowOf(tab.Id).Followed = null;
        host.Dispatch(new TabCommand(tab.Id, new SetCompareMarkCommand(selected)));
        session.Details.ClearComparison();
        Notice = $"Mark moved to {Short(selected)}";
    }

    private void Clear(RepositoryState tab)
    {
        FollowOf(tab.Id).Followed = null;
        host.Dispatch(new TabCommand(tab.Id, new SetCompareMarkCommand(null)));
        activeSession()?.Details.ClearComparison();
    }

    private TabFollow FollowOf(TabId tab)
    {
        if (!_follows.TryGetValue(tab, out var follow))
        {
            follow = new TabFollow();
            _follows[tab] = follow;
        }

        return follow;
    }

    private static string Short(ObjectId id) => id.ToString()[..HashLength];

    /// <summary>What <see cref="Follow"/> remembers per tab: the commit it asked for, and whether Changes was shown.</summary>
    private sealed class TabFollow
    {
        public ObjectId? Followed { get; set; }

        public bool ShownChanges { get; set; }
    }
}
