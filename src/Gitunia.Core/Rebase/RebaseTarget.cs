using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Rebase;

/// <summary>
/// What a rebase acts on, fixed when it is asked so a refresh while the dialog is open cannot redirect it.
/// </summary>
/// <param name="TabId">The tab.</param>
/// <param name="CommitHash">The commit to rebase onto.</param>
/// <param name="Label">How the commit is named in messages: the branch or tag, else its short hash.</param>
/// <param name="BranchName">The current branch that is rebased.</param>
/// <param name="OldTip">The branch tip before the rebase, for the undo.</param>
// ReSharper disable once NotAccessedPositionalProperty.Global : Kept for equality and the command log
public sealed record RebaseTarget(TabId TabId, string CommitHash, string Label, string BranchName, string OldTip);
