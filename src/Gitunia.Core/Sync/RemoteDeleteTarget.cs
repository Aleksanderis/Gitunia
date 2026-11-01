using System.Collections.Immutable;
using Gitunia.Core.Sidebar;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Sync;

/// <summary>
/// The remote branches the cursor names for a delete: a sidebar remote branch row, or the remote branches that point at
/// the selected graph commit. Decided from the tab refs, no git runs.
/// </summary>
public static class RemoteDeleteTarget
{
    /// <summary>The names (<c>origin/topic</c>) of the remote branches under the cursor, empty when there are none.</summary>
    public static IReadOnlyList<string> Names(ImmutableArray<GitRef> refs, SyncTarget chosen)
    {
        if (chosen.RowKind == SidebarRowKind.RemoteBranch)
        {
            return chosen.Name is { } name ? [name] : [];
        }

        if (chosen.RowKind is not null || chosen.CommitHash is not { } hash)
        {
            return [];
        }

        return
        [
            .. refs
                .Where(reference => reference.Kind == RefKind.RemoteBranch && reference.SymbolicTarget is null
                    && reference.CommitHash == hash)
                .Select(reference => reference.Name),
        ];
    }

    /// <summary>
    /// True when a branch delete on the cursor means a remote branch: a remote branch row, or a graph commit with
    /// remote branches and no local one (a local branch there keeps the plain local delete).
    /// </summary>
    public static bool IsRemoteOnly(ImmutableArray<GitRef> refs, SyncTarget chosen)
    {
        if (Names(refs, chosen).Count == 0)
        {
            return false;
        }

        return chosen.RowKind is not null
            || !refs.Any(reference => reference.Kind == RefKind.LocalBranch
                && reference.CommitHash == chosen.CommitHash);
    }
}
