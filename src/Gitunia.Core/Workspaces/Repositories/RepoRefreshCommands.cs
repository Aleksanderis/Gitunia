using System.Collections.Immutable;
using Gitunia.Git.Stash;
using Gitunia.Git.Worktrees;

namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>Worktrees were (re)loaded.</summary>
/// <param name="Worktrees">The worktrees, main one first.</param>
public sealed record WorktreesLoadedCommand(ImmutableArray<WorktreeInfo> Worktrees) : RepoCommand;

/// <summary>Stashes were (re)loaded.</summary>
/// <param name="Stashes">The stash stack, newest first.</param>
public sealed record StashesLoadedCommand(ImmutableArray<StashEntry> Stashes) : RepoCommand;
