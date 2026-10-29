using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Operations;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Merge;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Operations;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Merge;

/// <summary>
/// Merges the selected commit, branch or tag into the current branch: <c>gm</c> fast-forwards when possible, <c>gM</c>
/// always makes a merge commit and <c>gs</c> squashes (ux.md, workflow 3, "Integrating"). Nothing is lost by a merge,
/// so there is no Confirm; the status line names the old tip for the undo. A stop on conflicts is not a failure: the
/// in-progress operation takes over (banner, <c>gc</c>, <c>ga</c>). Refused on a detached or unborn HEAD and while
/// another operation is in progress. The git call runs off the UI thread, one operation per tab.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Runs the merge.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="refresh">Reads a tab again (refs, graph, status); false when it cannot.</param>
/// <param name="selection">What is selected now: a sidebar row or the graph commit.</param>
/// <param name="gate">The one-operation-per-tab gate, shared with staging, discard and reset.</param>
public sealed class MergeActions(
    WorkspaceHost host,
    IMergeWriter writer,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    Func<TabId, bool> refresh,
    Func<BranchSelection> selection,
    TabOperationGate gate)
{
    private const string BusyNotice = "Busy: another operation of this tab is still running";
    private const string SelectNotice = "Select a commit (graph) or a branch or tag (sidebar) to merge";

    /// <summary>The notice for the status bar after the last <see cref="TryExecute"/>, empty when none.</summary>
    public string Notice { get; private set; } = string.Empty;

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        var mode = ModeOf(action);
        if (mode is null)
        {
            return false;
        }

        if (host.Workspace.ActiveTab is { } tab)
        {
            Notice = Begin(tab, mode.Value);
        }

        return true;
    }

    private static MergeMode? ModeOf(ActionId action)
    {
        if (action == ActionIds.MergeBranch)
        {
            return MergeMode.Default;
        }

        if (action == ActionIds.MergeBranchNoFastForward)
        {
            return MergeMode.NoFastForward;
        }

        return action == ActionIds.MergeBranchSquash ? MergeMode.Squash : null;
    }

    private static string? Refusal(RepositoryState tab)
    {
        if (tab.Operation is { } operation)
        {
            var name = OperationBannerBuilder.NameOf(operation.Kind);
            return $"A {name} is in progress: finish or abort the {name} first (<space>gc continue, <space>ga abort)";
        }

        if (tab.Head is not { Kind: HeadKind.Branch, BranchName: not null })
        {
            if (tab.Head?.Kind == HeadKind.Unborn)
            {
                return "The branch has no commits yet: nothing to merge into";
            }

            return "HEAD is detached: switch to a branch first";
        }

        return null;
    }

    private static MergeTarget? TargetOf(BranchSelection chosen)
    {
        if (chosen.CommitHash is not { } hash)
        {
            return null;
        }

        // A branch is passed by its full ref, so a tag of the same name cannot win; the writer keeps the message
        // "Merge branch 'x'". A commit is passed by hash.
        var shortHash = OperationTexts.ShortHash(hash);
        return chosen.RowKind switch
        {
            null => new MergeTarget(hash, shortHash),
            SidebarRowKind.LocalBranch => new MergeTarget($"{GitRefPrefixes.Heads}{chosen.Name}", chosen.Name!),
            SidebarRowKind.RemoteBranch => new MergeTarget($"{GitRefPrefixes.Remotes}{chosen.Name}", chosen.Name!),
            SidebarRowKind.Tag => new MergeTarget(hash, chosen.Name ?? shortHash),
            _ => null,
        };
    }

    private string Begin(RepositoryState tab, MergeMode mode)
    {
        if (gate.IsBusy(tab.Id))
        {
            return BusyNotice;
        }

        if (Refusal(tab) is { } refusal)
        {
            return refusal;
        }

        if (TargetOf(selection()) is not { } target)
        {
            return SelectNotice;
        }

        gate.Begin(tab.Id);
        var branch = tab.Head!.BranchName!;
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => MergeAsync(tab, branch, target, mode, token), CancellationToken.None);
        return string.Empty;
    }

    private async Task MergeAsync(
        RepositoryState tab,
        string branch,
        MergeTarget target,
        MergeMode mode,
        CancellationToken token)
    {
        try
        {
            var result = await writer.MergeAsync(tab.Location, target.Revision, mode, token).ConfigureAwait(false);
            var report = MergeTexts.Report(result, mode, branch, target.Label);
            postToUi(() => Finish(tab.Id, report.Text, report.Severity, true));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => Finish(tab.Id, string.Empty, StatusSeverity.Info, false));
        }
        catch (Exception exception)
        {
            var failure = MergeTexts.Failure(branch, target.Label, OperationText.Reason(exception));
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error, true));
        }
    }

    private void Finish(TabId tabId, string text, StatusSeverity severity, bool refreshTab)
    {
        gate.End(tabId);
        statuses.Post(text, severity);
        if (refreshTab && host.Workspace.FindTab(tabId) is not null)
        {
            refresh(tabId);
        }
    }
}
