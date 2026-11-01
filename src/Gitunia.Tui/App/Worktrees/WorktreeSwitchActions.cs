using Gitunia.App;
using Gitunia.App.Tabs;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Worktrees;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Worktrees;

/// <summary>
/// Moves between worktrees (ux-workflows.md, workflow 6): <c>ws</c> points the active tab at the selected worktree in place, so
/// its view state stays and only HEAD, status and graph are read again; <c>wt</c> opens the worktree as its own tab (or
/// focuses the tab that already shows it); on a local branch row <c>wt</c> opens the worktree that holds the branch.
/// Nothing here changes the process working directory: the new location is
/// resolved by path and every git call keeps taking the tab's own path. Refusals run no git and say why.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="discovery">Resolves the path of a worktree to its location.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="gate">The one-operation-per-tab gate; a switch waits for a running operation to end.</param>
/// <param name="selectedRow">The sidebar row under the cursor of the active tab.</param>
public sealed class WorktreeSwitchActions(
    WorkspaceHost host,
    RepositoryDiscovery discovery,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    TabOperationGate gate,
    Func<SidebarRow?> selectedRow)
{
    private const string NoWorktreeNotice = "Select a worktree in the sidebar first";
    private const string BusyNotice = "Busy: another operation on this tab is still running";

    /// <summary>The status line text of the last action; empty when it started work or did nothing to say.</summary>
    public string Notice { get; private set; } = string.Empty;

    public bool TryExecute(ActionId action)
    {
        Notice = string.Empty;
        var isSwitch = action == ActionIds.WorktreeSwitch;
        if (!isSwitch && action != ActionIds.WorktreeOpen)
        {
            return false;
        }

        if (host.Workspace.ActiveTab is not { } tab)
        {
            return true;
        }

        if (!isSwitch && IsBranchWithoutWorktree(tab))
        {
            return false;
        }

        Notice = Begin(tab, isSwitch);
        return true;
    }

    /// <summary>Opens the worktree at a path as its own tab (or focuses the tab that shows it), reporting failures.</summary>
    /// <param name="tabId">The tab whose cancellation ends the work.</param>
    /// <param name="path">The worktree directory.</param>
    public void OpenPath(TabId tabId, string path)
    {
        var token = cancellationOf(tabId);
        _ = Task.Run(() => DiscoverAsync(tabId, path, false, token), CancellationToken.None);
    }

    private string Begin(RepositoryState tab, bool isSwitch)
    {
        if (isSwitch && gate.IsBusy(tab.Id))
        {
            return BusyNotice;
        }

        if (TargetOf(tab) is not { } worktree)
        {
            return NoWorktreeNotice;
        }

        var refusal = Refusal(tab, worktree, isSwitch);
        if (refusal.Length > 0)
        {
            return refusal;
        }

        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => DiscoverAsync(tab.Id, worktree.Path, isSwitch, token), CancellationToken.None);
        return string.Empty;
    }

    private string Refusal(RepositoryState tab, WorktreeInfo worktree, bool isSwitch)
    {
        if (worktree.IsBare)
        {
            return "The bare repository has no working tree to switch to";
        }

        if (worktree.IsPrunable)
        {
            return $"{worktree.Path} is gone from disk: prune it with <space>wp";
        }

        if (RepositoryPathComparer.Instance.Equals(worktree.Path, tab.CommandPath))
        {
            return "This tab already shows that worktree";
        }

        if (isSwitch && OtherTabShowing(tab, worktree.Path) is { } other)
        {
            return $"{worktree.Path} is already open in tab {other.Title}: <space>wt goes there";
        }

        return string.Empty;
    }

    private async Task DiscoverAsync(TabId tabId, string path, bool isSwitch, CancellationToken token)
    {
        try
        {
            var location = await discovery.DiscoverAsync(path, token).ConfigureAwait(false);
            postToUi(() => Apply(tabId, location, isSwitch));
        }
        catch (OperationCanceledException)
        {
            // The tab was closed.
        }
        catch (Exception exception)
        {
            var verb = isSwitch ? "switch to" : "open";
            postToUi(() => statuses.Error($"Could not {verb} {path}: {OperationText.Reason(exception)}"));
        }
    }

    // Runs on the UI thread: the workspace may have changed while the location was resolved, so all is checked again.
    private void Apply(TabId tabId, RepositoryLocation location, bool isSwitch)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return;
        }

        if (!isSwitch)
        {
            host.Dispatch(new OpenRepositoryCommand(location));
            statuses.Post($"Opened worktree {location.RootPath}");
            return;
        }

        if (gate.IsBusy(tabId))
        {
            statuses.Warning($"{BusyNotice}; the tab stays on {tab.Location.RootPath}");
            return;
        }

        if (OtherTabShowing(tab, location.RootPath) is { } other)
        {
            statuses.Warning($"{location.RootPath} is already open in tab {other.Title}: the tab stays where it is");
            return;
        }

        host.Dispatch(new SwitchWorktreeCommand(tabId, location));
        statuses.Post($"Switched to worktree {location.RootPath}");
    }

    private RepositoryState? OtherTabShowing(RepositoryState tab, string path) =>
        host.Workspace.Tabs.FirstOrDefault(
            other => other.Id != tab.Id && RepositoryPathComparer.Instance.Equals(other.Location.RootPath, path));

    private bool IsBranchWithoutWorktree(RepositoryState tab) =>
        selectedRow() is { Kind: SidebarRowKind.LocalBranch } && HolderOf(tab) is null;

    private WorktreeInfo? TargetOf(RepositoryState tab)
    {
        var path = selectedRow() switch
        {
            { Kind: SidebarRowKind.Worktree, Path: { } worktreePath } => worktreePath,
            { Kind: SidebarRowKind.LocalBranch } => HolderOf(tab),
            _ => null,
        };

        if (path is null)
        {
            return null;
        }

        return tab.Worktrees.FirstOrDefault(worktree => RepositoryPathComparer.Instance.Equals(worktree.Path, path));
    }

    // The path of the worktree that has the selected local branch checked out, null when none has.
    private string? HolderOf(RepositoryState tab)
    {
        var name = selectedRow()?.Label;
        var reference = tab.Refs.FirstOrDefault(
            candidate => candidate.Kind == RefKind.LocalBranch && candidate.Name == name);
        return reference?.CheckedOutIn ?? (reference?.IsCurrent == true ? tab.CommandPath : null);
    }
}
