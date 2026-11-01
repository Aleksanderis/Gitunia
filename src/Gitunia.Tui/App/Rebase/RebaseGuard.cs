using Gitunia.Core.Operations;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Rebase;

/// <summary>
/// The refusals and warnings every history-rewriting rebase of the graph shares (the rebase onto a commit and the quick
/// fixup, reword and drop). Decided from the tab state, no git runs.
/// </summary>
public static class RebaseGuard
{
    /// <summary>
    /// Why a rebase must not start in this tab, or null: an operation is in progress, HEAD is not on a branch with
    /// commits, <paramref name="sameCommit"/> is where the branch already is, or tracked files have changes
    /// (unless the user's <c>rebase.autoStash</c> lets git carry them over).
    /// </summary>
    public static string? Refusal(RepositoryState tab, string? sameCommit)
    {
        if (tab.Operation is { } info)
        {
            var name = OperationBannerBuilder.NameOf(info.Kind);
            return $"A {name} is in progress: finish or abort the {name} first (<space>gc continue, <space>ga abort)";
        }

        if (tab.Head is not { Kind: HeadKind.Branch, BranchName: not null, CommitHash: not null })
        {
            return tab.Head?.Kind switch
            {
                null => "The repository is still loading: try again in a moment",
                HeadKind.Unborn => "The branch has no commits yet: nothing to rebase",
                _ => "HEAD is detached: switch to a branch first",
            };
        }

        if (sameCommit is not null && string.Equals(tab.Head.CommitHash, sameCommit, StringComparison.Ordinal))
        {
            return $"{tab.Head.BranchName} is already at that commit: nothing to rebase";
        }

        if (tab.RebaseAutoStash)
        {
            return null;
        }

        var changed = ChangedCount(tab);
        if (changed == 0)
        {
            return null;
        }

        var files = OperationText.Plural(changed, "file");
        return $"{files} with uncommitted changes: commit or stash them before a rebase";
    }

    /// <summary>
    /// The Confirm line saying git carries the uncommitted changes over the rebase, or null when
    /// <c>rebase.autoStash</c> is off or there is nothing to carry.
    /// </summary>
    public static string? AutoStashNote(RepositoryState? tab)
    {
        var changed = tab is { RebaseAutoStash: true } ? ChangedCount(tab) : 0;
        if (changed == 0)
        {
            return null;
        }

        var files = OperationText.Plural(changed, "file");
        return $"{files} with uncommitted changes: git stashes them (rebase.autoStash) " +
            "and restores them afterwards; if that conflicts, the stash is kept.";
    }

    /// <summary>The force-push warning for a branch that tracks an upstream, or null.</summary>
    public static string? UpstreamWarning(GitRef? current)
    {
        if (current is not { UpstreamFullName: { } upstream, IsUpstreamGone: false })
        {
            return null;
        }

        var pushed = current.Ahead == 0 ? "All of its commits are already on" : "It tracks";
        return $"{pushed} {ShortRef(upstream)}: the history is rewritten and a force push is needed afterwards.";
    }

    private static int ChangedCount(RepositoryState tab) =>
        tab.WorkingTree?.Snapshot.Entries.Count(IsTrackedChange) ?? 0;

    private static bool IsTrackedChange(StatusEntry entry) =>
        entry.Kind is not (StatusEntryKind.Untracked or StatusEntryKind.Ignored);

    private static string ShortRef(string fullName)
    {
        if (fullName.StartsWith(GitRefPrefixes.Remotes, StringComparison.Ordinal))
        {
            return fullName[GitRefPrefixes.Remotes.Length..];
        }

        if (fullName.StartsWith(GitRefPrefixes.Heads, StringComparison.Ordinal))
        {
            return fullName[GitRefPrefixes.Heads.Length..];
        }

        return fullName;
    }
}
