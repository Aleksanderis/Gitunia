using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Reset;

namespace Gitunia.Tui.App.Reset;

/// <summary>What one reset acts on, fixed when the mode is chosen.</summary>
/// <param name="TabId">The tab whose branch moves.</param>
/// <param name="CommitHash">The commit it moves to.</param>
/// <param name="BranchName">The branch that moves, for the texts.</param>
/// <param name="Mode">Soft, mixed or hard.</param>
public sealed record ResetTarget(TabId TabId, string CommitHash, string BranchName, ResetMode Mode);
