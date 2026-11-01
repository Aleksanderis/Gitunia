using System.Collections.Immutable;
using Gitunia.Git.Stash;

namespace Gitunia.Core.Stash;

/// <summary>
/// The decisions of the stash view (#699): which stash a sidebar row shows and what the stash stack does to the shown
/// one. Stashes are known by their commit hash, never by position, so the pane never switches to another stash.
/// </summary>
public static class StashViewRules
{
    /// <summary>The stash with the commit, null when the stack no longer has it.</summary>
    public static StashEntry? Find(ImmutableArray<StashEntry> stashes, string? commitHash) =>
        stashes.FirstOrDefault(entry => entry.CommitHash == commitHash);

    /// <summary>What the stack did to the stash that is shown.</summary>
    public static StashViewChange Reconcile(StashEntry shown, ImmutableArray<StashEntry> stashes)
    {
        if (Find(stashes, shown.CommitHash) is not { } current)
        {
            return new StashViewChange(StashViewChangeKind.Gone, null);
        }

        return current == shown
            ? new StashViewChange(StashViewChangeKind.Unchanged, current)
            : new StashViewChange(StashViewChangeKind.Renamed, current);
    }

    /// <summary>The warning for a shown stash that left the stack.</summary>
    public static string GoneNotice(StashEntry shown) => $"{shown.Name} is no longer in the stash list";

    /// <summary>
    /// Whether to warn that the shown stash is gone: not when the status bar already names it (a drop or pop from this
    /// app reported it with the restore command, which must stay).
    /// </summary>
    public static bool ShouldWarnGone(StashEntry shown, string? currentStatusText) =>
        currentStatusText?.Contains(shown.Name, StringComparison.Ordinal) != true;
}
