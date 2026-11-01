using System.Collections.Immutable;
using Gitunia.Core.Workspaces;

namespace Gitunia.Core.Worktrees;

/// <summary>The outcome of a worktree command: the effects to run and, when a worktree was located or its overview
/// read, the workspace command that applies it.</summary>
/// <param name="Effects">The effects to run.</param>
/// <param name="Workspace">The workspace command to reduce next, null for none.</param>
public sealed record WorktreeUpdate(ImmutableArray<AppEffect> Effects, WorkspaceCommand? Workspace = null)
{
    /// <summary>An update that only returns effects.</summary>
    public static implicit operator WorktreeUpdate(ImmutableArray<AppEffect> effects) => new(effects);
}
