using System.Collections.Immutable;
using Gitunia.Git;

namespace Gitunia.Core.Workspaces;

/// <summary>A change to one tab's <see cref="RepoState"/>.</summary>
public abstract record RepoCommand
{
    private protected RepoCommand()
    {
    }
}

/// <summary>Select a commit (null selects the newest).</summary>
/// <param name="Commit">The commit id.</param>
public sealed record SelectCommitCommand(ObjectId? Commit) : RepoCommand;

/// <summary>Set the first visible graph row; negative values clamp to zero.</summary>
/// <param name="Offset">The row index.</param>
public sealed record ScrollToCommand(int Offset) : RepoCommand;

/// <summary>Set the graph filter text.</summary>
/// <param name="Text">The filter; empty clears it.</param>
public sealed record SetFilterCommand(string Text) : RepoCommand;

/// <summary>Move focus to a pane.</summary>
/// <param name="Pane">The pane.</param>
public sealed record FocusPaneCommand(PaneKind Pane) : RepoCommand;

/// <summary>HEAD was (re)loaded.</summary>
/// <param name="Head">The new HEAD state.</param>
public sealed record HeadLoadedCommand(HeadState Head) : RepoCommand;

/// <summary>Refs were (re)loaded.</summary>
/// <param name="Refs">The new refs.</param>
public sealed record RefsLoadedCommand(ImmutableArray<GitRef> Refs) : RepoCommand;

/// <summary>The repository directory vanished or came back.</summary>
/// <param name="IsMissing">Whether it is missing.</param>
public sealed record SetMissingCommand(bool IsMissing) : RepoCommand;

/// <summary>The working tree status was (re)loaded.</summary>
/// <param name="Summary">What the tab bar shows.</param>
public sealed record StatusLoadedCommand(RepoStatusSummary Summary) : RepoCommand;
