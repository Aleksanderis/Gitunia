using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.App.Compare;

/// <summary>
/// Compares two commits of the graph (#535, ux.md "Comparing two commits"). <c>&lt;space&gt;=</c> marks the selected
/// commit; on another commit it shows <c>git diff older newer</c> (older and newer by graph order) in the Changes tab
/// and the diff pane, and on the marked commit it clears the mark. <c>Esc</c> in the graph clears the mark and the
/// comparison. The mark is part of the tab's state, so each tab has its own; selecting another commit ends the
/// comparison shown but keeps the mark. Runs without a terminal.
/// </summary>
/// <param name="host">The workspace, which holds the mark of each tab.</param>
/// <param name="activeSession">The active tab's session, null when there is none.</param>
/// <param name="selectedCommit">The commit under the graph cursor, null when none.</param>
public sealed class CompareActions(WorkspaceHost host, Func<TabSession?> activeSession, Func<ObjectId?> selectedCommit)
{
    private const int HashLength = 7;

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
            host.Dispatch(new TabCommand(tab.Id, new SetCompareMarkCommand(selected)));
            Notice = $"Marked {Short(selected)}: select another commit and press <space>= to compare";
            return;
        }

        if (mark == selected)
        {
            Clear(tab);
            Notice = "Comparison mark cleared";
            return;
        }

        var markIndex = session.Graph.IndexOf(mark);
        var selectedIndex = session.Graph.IndexOf(selected);
        if (markIndex < 0 || selectedIndex < 0)
        {
            Notice = "The marked commit is not in the graph (Esc clears the mark)";
            return;
        }

        // The graph lists the newest commit first, so the larger index is the older commit.
        var older = markIndex > selectedIndex ? mark : selected;
        var newer = markIndex > selectedIndex ? selected : mark;
        if (!session.Details.Compare(older, newer))
        {
            Notice = "Still loading the commit; try again";
            return;
        }

        host.Dispatch(new TabCommand(tab.Id, new SetBottomTabCommand(BottomTab.Changes)));
        Notice = $"Comparing {Short(older)}..{Short(newer)}";
    }

    private void Clear(RepositoryState tab)
    {
        host.Dispatch(new TabCommand(tab.Id, new SetCompareMarkCommand(null)));
        activeSession()?.Details.ClearComparison();
    }

    private static string Short(ObjectId id) => id.ToString()[..HashLength];
}
