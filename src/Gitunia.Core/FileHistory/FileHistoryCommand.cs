using Gitunia.Core.Diff;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.History.Models;

namespace Gitunia.Core.FileHistory;

/// <summary>A command on the file history overlay: user intent and the results of its effects.</summary>
public abstract record FileHistoryCommand : AppCommand;

/// <summary>Open the history of a file.</summary>
/// <param name="TabId">The tab whose repository holds the file.</param>
/// <param name="Path">The repository-relative path of the file.</param>
/// <param name="Revision">Where the history starts: the shown commit, or <c>HEAD</c>.</param>
/// <param name="Range">The lines of the file (as they are at <paramref name="Revision"/>) to show the history of; null
/// for the whole file.</param>
public sealed record FileHistoryOpenRequested(
    TabId TabId,
    string Path,
    string Revision,
    LineRange? Range = null) : FileHistoryCommand;

/// <summary>A bound action while the history is open (it consumes every action).</summary>
/// <param name="Action">The action.</param>
/// <param name="Count">The numeric prefix, null when none was typed.</param>
/// <param name="PageRows">How many rows the list shows, for page motions.</param>
public sealed record FileHistoryActionPressed(ActionId Action, int? Count, int PageRows) : FileHistoryCommand;

/// <summary>A click on a commit row.</summary>
/// <param name="Index">The commit.</param>
public sealed record FileHistoryEntryClicked(int Index) : FileHistoryCommand;

/// <summary>The tab of the history was closed: the history closes.</summary>
public sealed record FileHistoryTabClosed : FileHistoryCommand;

/// <summary>The list read finished.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Generation">The read number of the effect.</param>
/// <param name="Path">The file.</param>
/// <param name="Revision">Where the walk started.</param>
/// <param name="Entries">The commits, newest first.</param>
/// <param name="Range">The lines the walk was asked for, null for the whole file.</param>
public sealed record FileHistoryLoaded(
    TabId TabId,
    long Generation,
    string Path,
    string Revision,
    IReadOnlyList<FileHistoryEntry> Entries,
    LineRange? Range = null) : FileHistoryCommand;

/// <summary>The list read failed.</summary>
/// <param name="Text">What to tell the user.</param>
public sealed record FileHistoryReadFailed(string Text) : FileHistoryCommand;

/// <summary>The list read was cancelled (its tab closed).</summary>
public sealed record FileHistoryReadCancelled : FileHistoryCommand;

/// <summary>A diff read finished.</summary>
/// <param name="Generation">The diff read number of the effect.</param>
/// <param name="Document">The diff of the file in the commit.</param>
public sealed record FileHistoryDiffLoaded(long Generation, DiffDocument Document) : FileHistoryCommand;

/// <summary>A diff read failed.</summary>
/// <param name="Generation">The diff read number of the effect.</param>
/// <param name="Text">What to tell the user.</param>
public sealed record FileHistoryDiffFailed(long Generation, string Text) : FileHistoryCommand;

/// <summary>A diff read was cancelled by a newer one or by its tab closing.</summary>
public sealed record FileHistoryDiffCancelled : FileHistoryCommand;

/// <summary>The graph cursor moved to the commit: the history closes.</summary>
public sealed record FileHistoryJumpDone : FileHistoryCommand;

/// <summary>The graph cursor could not move: the history stays and the status says why.</summary>
/// <param name="Text">The reason.</param>
public sealed record FileHistoryJumpRefused(string Text) : FileHistoryCommand;
