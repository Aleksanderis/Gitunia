using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Rebase;

namespace Gitunia.Core.Rebase;

/// <summary>
/// What one quick rebase acts on, fixed when it is asked so a refresh while the dialog is open cannot redirect it.
/// </summary>
/// <param name="TabId">The tab.</param>
/// <param name="Plan">What the writer read: the commit, its range and the HEAD it was read at.</param>
/// <param name="BranchName">The current branch that is rewritten.</param>
/// <param name="NewSubject">The typed subject of a reword; null for the other edits.</param>
// ReSharper disable once NotAccessedPositionalProperty.Global : Kept for equality and the command log
public sealed record QuickRebaseTarget(TabId TabId, QuickRebasePlan Plan, string BranchName, string? NewSubject);
