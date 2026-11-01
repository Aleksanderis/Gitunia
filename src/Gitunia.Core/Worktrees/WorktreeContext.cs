using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Worktrees;

/// <summary>The facts of the root the worktree reducer reads: the tab, every open tab and whether a worktree operation
/// already runs for the tab.</summary>
/// <param name="Tab">The tab the command is for.</param>
/// <param name="Tabs">Every open tab, to refuse switching to a worktree another tab shows.</param>
/// <param name="IsBusy">A worktree operation already runs for the tab (read from the tab's running operations).</param>
public sealed record WorktreeContext(RepositoryState Tab, ImmutableArray<RepositoryState> Tabs, bool IsBusy);
