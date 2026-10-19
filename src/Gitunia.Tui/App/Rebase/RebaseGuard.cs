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
    private const int ShortHashLength = 7;

    /// <summary>The first seven characters of a hash.</summary>
    public static string ShortHash(string hash) => hash[..Math.Min(ShortHashLength, hash.Length)];

    /// <summary>
    /// Why a rebase must not start in this tab, or null: an operation is in progress, HEAD is not on a branch with
    /// commits, <paramref name="sameCommit"/> is where the branch already is, or tracked files have changes.
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

        var changed = tab.WorkingTree?.Snapshot.Entries.Count(IsTrackedChange) ?? 0;
        return changed == 0
            ? null
            : $"{OperationText.Plural(changed, "file")} with uncommitted changes: commit or stash them before a rebase";
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

    private static bool IsTrackedChange(StatusEntry entry) =>
        entry.Kind is not (StatusEntryKind.Untracked or StatusEntryKind.Ignored);

    private static string ShortRef(string fullName) =>
        fullName.StartsWith(GitRefPrefixes.Remotes, StringComparison.Ordinal)
            ? fullName[GitRefPrefixes.Remotes.Length..]
            : fullName.StartsWith(GitRefPrefixes.Heads, StringComparison.Ordinal)
                ? fullName[GitRefPrefixes.Heads.Length..]
                : fullName;
}
