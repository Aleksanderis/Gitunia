using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Persistence;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Core.Commits;

/// <summary>A command on the commit message box of one tab or the result of one of its effects. Dropped when the tab is
/// no longer open (a late result, an answer after the tab was closed).</summary>
public abstract record CommitCommand : AppCommand
{
    private protected CommitCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>A bound action of the message box: write or edit the message, its Normal mode keys, a new line, commit, the
/// options and Esc (stop editing or cancel a running commit).</summary>
/// <param name="TabId">The active tab, showing the Changes view.</param>
/// <param name="Action">The action.</param>
public sealed record CommitActionPressed(TabId TabId, ActionId Action) : CommitCommand(TabId);

/// <summary>A key typed in Insert mode while the box owns it.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Key">The key.</param>
public sealed record CommitKeyTyped(TabId TabId, KeyEvent Key) : CommitCommand(TabId);

/// <summary>A click on the message text: the cursor moves to the line and column.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Row">The line of the message.</param>
/// <param name="Column">The display column in the line, the sideways scroll already added.</param>
public sealed record CommitCursorPlaced(TabId TabId, int Row, int Column) : CommitCommand(TabId);

/// <summary><c>y</c> in the "stage all and commit" question; the message and options are those the user saw.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Request">What to commit.</param>
public sealed record CommitStageAllConfirmed(TabId TabId, CommitRequest Request) : CommitCommand(TabId);

/// <summary>The files were staged for a confirmed "stage all and commit": commit them.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Request">What to commit.</param>
public sealed record CommitAfterStaging(TabId TabId, CommitRequest Request) : CommitCommand(TabId);

/// <summary>A saved draft is put into its tab (a restart; the tab has no draft yet).</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Saved">The draft read from the session file.</param>
public sealed record CommitDraftRestored(TabId TabId, SessionDraft Saved) : CommitCommand(TabId);

/// <summary>The unsent draft of a tab that is about to be replaced (a located missing tab) moves to its replacement;
/// the
/// root reducer reduces <c>PickerDraftMoved</c> right after. Touches two tabs, so it is no <see
/// cref="CommitCommand"/>.</summary>
/// <param name="Missing">The tab that is replaced.</param>
/// <param name="Target">The tab that replaces it.</param>
public sealed record CommitDraftMoveRequested(TabId Missing, TabId Target) : AppCommand;

/// <summary>Result: the commit was created.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Request">What was committed.</param>
/// <param name="Result">The new commit.</param>
/// <param name="ForcePushUpstream">The upstream an amended commit was already on, null otherwise.</param>
public sealed record CommitSucceeded(TabId TabId, CommitRequest Request, CommitResult Result, string? ForcePushUpstream)
    : CommitCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Commit;
}

/// <summary>Result: the commit failed or was cancelled; the draft stays.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Notice">The status bar text.</param>
/// <param name="Output">Output of git and the hooks for the output overlay, null when there is none.</param>
public sealed record CommitFailed(TabId TabId, string Notice, IReadOnlyList<string>? Output)
    : CommitCommand(TabId), IOperationResult
{
    RunningOperationKind IOperationResult.Kind => RunningOperationKind.Commit;
}

/// <summary>Result: the message of HEAD was read for an amend.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Hash">HEAD when the read started, null when unknown.</param>
/// <param name="Message">The full message.</param>
public sealed record HeadMessageRead(TabId TabId, string? Hash, string Message) : CommitCommand(TabId);

/// <summary>Result: the message of HEAD could not be read.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Notice">What to tell the user.</param>
public sealed record HeadMessageReadFailed(TabId TabId, string Notice) : CommitCommand(TabId);

/// <summary>Result: the message prepared for finishing a merge was read.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Message">The message.</param>
public sealed record MergeMessageRead(TabId TabId, string Message) : CommitCommand(TabId);
