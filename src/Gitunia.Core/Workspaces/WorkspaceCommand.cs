using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Workspaces;

/// <summary>A change to the workspace as a whole.</summary>
public abstract record WorkspaceCommand : AppCommand
{
    private protected WorkspaceCommand()
    {
    }
}

/// <summary>Open a repository in a new tab, or focus its tab when it is already open.</summary>
/// <param name="Location">The resolved repository location.</param>
public sealed record OpenRepositoryCommand(RepositoryLocation Location) : WorkspaceCommand;

/// <summary>
/// Point a tab at another worktree of its repository, in place: the view state (selection, scroll, panes, filters)
/// stays, HEAD, status and graph are read again for the new worktree. Does nothing when another tab already shows that
/// worktree.
/// </summary>
/// <param name="TabId">The tab to move.</param>
/// <param name="Location">The resolved location of the worktree.</param>
public sealed record SwitchWorktreeCommand(TabId TabId, RepositoryLocation Location) : WorkspaceCommand;

/// <summary>Close a tab; the neighbour becomes active.</summary>
/// <param name="TabId">The tab to close.</param>
public sealed record CloseTabCommand(TabId TabId) : WorkspaceCommand;

/// <summary>Make a tab active.</summary>
/// <param name="TabId">The tab to focus.</param>
public sealed record ActivateTabCommand(TabId TabId) : WorkspaceCommand;

/// <summary>Activate the tab after the active one, wrapping around.</summary>
public sealed record NextTabCommand : WorkspaceCommand;

/// <summary>Activate the tab before the active one, wrapping around.</summary>
public sealed record PreviousTabCommand : WorkspaceCommand;

/// <summary>Activate the tab at a one-based position (<c>3gt</c>, <c>Alt-3</c>); out of range does nothing.</summary>
/// <param name="Position">One-based tab position.</param>
public sealed record GoToTabCommand(int Position) : WorkspaceCommand;

/// <summary>
/// Move the active tab by a number of places (negative is left); the tab stays active, the move stops at either end
/// (no wrap) and nothing happens when there is no active tab or the offset is zero.
/// </summary>
/// <param name="Offset">Places to move: negative left, positive right.</param>
public sealed record MoveTabCommand(int Offset) : WorkspaceCommand;

/// <summary>Activate the previously active tab.</summary>
public sealed record ActivatePreviousTabCommand : WorkspaceCommand;

/// <summary>Pin the repository, or unpin it when it is pinned.</summary>
/// <param name="RootPath">The repository root path.</param>
public sealed record TogglePinCommand(string RootPath) : WorkspaceCommand;

/// <summary>Forget a repository in the recents list (nothing on disk is touched).</summary>
/// <param name="RootPath">The repository root path.</param>
public sealed record RemoveRecentCommand(string RootPath) : WorkspaceCommand;

/// <summary>A command for one tab, routed by id. Dropped when the tab is no longer open (a late git result).</summary>
/// <param name="TabId">The target tab.</param>
/// <param name="Command">The command for that tab's repository state.</param>
public sealed record TabCommand(TabId TabId, RepositoryCommand Command) : WorkspaceCommand;
