using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Git;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>A change to one tab's <see cref="RepositoryState"/>.</summary>
public abstract record RepositoryCommand
{
    private protected RepositoryCommand()
    {
    }
}

/// <summary>Select a commit (null selects the newest).</summary>
/// <param name="Commit">The commit id.</param>
public sealed record SelectCommitCommand(ObjectId? Commit) : RepositoryCommand;

/// <summary>Set the first visible graph row; negative values clamp to zero.</summary>
/// <param name="Offset">The row index.</param>
public sealed record ScrollToCommand(int Offset) : RepositoryCommand;

/// <summary>Set the graph filter text.</summary>
/// <param name="Text">The filter; empty clears it.</param>
public sealed record SetFilterCommand(string Text) : RepositoryCommand;

/// <summary>Set the pane sizes the user chose (null is automatic).</summary>
/// <param name="SidebarWidth">Columns of the sidebar.</param>
/// <param name="BottomHeight">Rows of the bottom strip.</param>
/// <param name="DetailWidth">Columns of the detail pane beside the diff.</param>
public sealed record SetPaneSizesCommand(int? SidebarWidth, int? BottomHeight, int? DetailWidth) : RepositoryCommand;

/// <summary>Move focus to a pane.</summary>
/// <param name="Pane">The pane.</param>
public sealed record FocusPaneCommand(PaneKind Pane) : RepositoryCommand;

/// <summary>HEAD was (re)loaded.</summary>
/// <param name="Head">The new HEAD state.</param>
public sealed record HeadLoadedCommand(HeadState Head) : RepositoryCommand;

/// <summary>Refs were (re)loaded.</summary>
/// <param name="Refs">The new refs.</param>
public sealed record RefsLoadedCommand(ImmutableArray<GitRef> Refs) : RepositoryCommand;

/// <summary>The repository directory vanished or came back.</summary>
/// <param name="IsMissing">Whether it is missing.</param>
public sealed record SetMissingCommand(bool IsMissing) : RepositoryCommand;

/// <summary>The working tree status was (re)loaded.</summary>
/// <param name="Summary">What the tab bar shows.</param>
public sealed record StatusLoadedCommand(RepositoryStatusSummary Summary) : RepositoryCommand;
