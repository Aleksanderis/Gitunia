using System.Collections.Immutable;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Worktrees;

namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>Worktrees were (re)loaded.</summary>
/// <param name="Worktrees">The worktrees, main one first.</param>
public sealed record WorktreesLoadedCommand(ImmutableArray<WorktreeInfo> Worktrees) : RepositoryCommand;

/// <summary>Stashes were (re)loaded.</summary>
/// <param name="Stashes">The stash stack, newest first.</param>
public sealed record StashesLoadedCommand(ImmutableArray<StashEntry> Stashes) : RepositoryCommand;

/// <summary>The details of the in-progress operation were (re)read.</summary>
/// <param name="Operation">The operation the banner shows, null when none is in progress.</param>
public sealed record OperationLoadedCommand(OperationInfo? Operation) : RepositoryCommand;
