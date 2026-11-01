using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Pick;

namespace Gitunia.Core.Pick;

/// <summary>
/// The open question "which parent is the mainline" of a cherry-pick or revert that has a merge commit among its
/// commits. It belongs to <see cref="TabId"/> and is dropped when that tab is no longer the active one.
/// </summary>
/// <param name="TabId">The tab the question was asked in.</param>
/// <param name="Kind">Cherry-pick or revert.</param>
/// <param name="Commits">The commits in apply order.</param>
/// <param name="MainlineCount">How many parents the merge commit with the fewest parents has.</param>
/// <param name="UsableParents">
/// How many parents from the first can be chosen: the call fails as a whole on <c>-m 2</c> or higher when a plain
/// commit is among the commits.
/// </param>
public sealed record MainlineQuestion(
    TabId TabId,
    PickKind Kind,
    ImmutableArray<PickCommit> Commits,
    int MainlineCount,
    int UsableParents)
{
    /// <summary>The first merge commit, the one the question is worded for.</summary>
    public PickCommit Merge => Commits.First(commit => commit.IsMerge);
}
