using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Blame.Models;

namespace Gitunia.Core.Blame;

/// <summary>A command on the blame view: user intent and the results of its effects.</summary>
public abstract record BlameCommand : AppCommand;

/// <summary>Open the view for a file of the active tab.</summary>
/// <param name="Path">The file, relative to the repository root.</param>
/// <param name="Revision">The commit to read it at, null for the working tree file.</param>
public sealed record BlameOpenRequested(string Path, string? Revision) : BlameCommand;

/// <summary>The terminal layer found nothing to blame under the cursor.</summary>
/// <param name="Text">Why, for the status bar.</param>
public sealed record BlameOpenRefused(string Text) : BlameCommand;

/// <summary>A bound action while the view is open (it consumes every action).</summary>
/// <param name="Action">The action.</param>
/// <param name="Count">The numeric prefix, null when none was typed.</param>
/// <param name="PageRows">How many lines the view shows, for page motions.</param>
/// <param name="CursorCommit">The commit of the highlighted line as the user sees it, null while git has not found it
/// yet (the view reads it from the store; the reducer cannot).</param>
/// <param name="CursorPath">The file's path in the parent of that commit (its path in the commit when none other is
/// named).</param>
/// <param name="CursorOriginalLine">The line number in that commit's version of the file (zero-based).</param>
/// <param name="CursorIsBoundary">True when that commit is a root of the walked history (it has no parent).</param>
/// <param name="CursorOwnPath">The file's path in that commit itself, which the line history starts from.</param>
/// <param name="CursorText">The text of the highlighted line, for the character motions; empty when unknown.</param>
/// <param name="RangeLines">For the line history of a selection: how many lines it covers (the cursor values above
/// then describe its first line).</param>
/// <param name="RangeIsOneCommit">For the line history of a selection: all its lines come from the same commit and are
/// consecutive in it.</param>
public sealed record BlameActionPressed(
    ActionId Action,
    int? Count,
    int PageRows,
    string? CursorCommit,
    string? CursorPath = null,
    int CursorOriginalLine = 0,
    bool CursorIsBoundary = false,
    string? CursorOwnPath = null,
    string CursorText = "",
    int RangeLines = 1,
    bool RangeIsOneCommit = true) : BlameCommand;

/// <summary>The mouse selected characters of the code column (a drag, a double or triple click).</summary>
/// <param name="Anchor">Where the selection starts.</param>
/// <param name="Top">The first line shown when the pointer moved.</param>
/// <param name="Cursor">Where its moving end is.</param>
public sealed record BlameTextSelected(TextPosition Anchor, TextPosition Cursor, int Top) : BlameCommand;

/// <summary>
/// Copy the selection of the view without a key: copy on select (a mouse release or a double or triple click) and
/// <c>Ctrl-c</c> keep the selection, <c>y</c> (a bound action) ends it.
/// </summary>
/// <param name="Mode">Why it is copied.</param>
public sealed record BlameCopyRequested(VisualCopyMode Mode) : BlameCommand;

/// <summary>The mouse selected whole lines (a drag that started in the gutter).</summary>
/// <param name="Anchor">The line the drag started on.</param>
/// <param name="Top">The first line shown when the pointer moved.</param>
/// <param name="Cursor">The line under the pointer.</param>
public sealed record BlameLinesSelected(int Anchor, int Cursor, int Top) : BlameCommand;

/// <summary>A key typed into the search box.</summary>
/// <param name="Key">The key.</param>
public sealed record BlameKeyTyped(KeyEvent Key) : BlameCommand;

/// <summary>A click on a line.</summary>
/// <param name="Top">The first line shown when the pointer clicked.</param>
/// <param name="Index">The line.</param>
public sealed record BlameRowClicked(int Index, int Top) : BlameCommand;

/// <summary>The lines of the file are known (or the file cannot be blamed).</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Generation">The read number of the effect.</param>
/// <param name="Kind">Text, or why there are no lines.</param>
/// <param name="LineCount">The lines of the file.</param>
/// <param name="IgnoreRevsFile">The <c>blame.ignoreRevsFile</c> config value that git blame honors, null when none
/// is set.</param>
public sealed record BlameLoaded(
    TabId TabId,
    long Generation,
    BlameContentKind Kind,
    int LineCount,
    string? IgnoreRevsFile = null) : BlameCommand;

/// <summary>Git found the commits of more lines.</summary>
/// <param name="Generation">The read number.</param>
/// <param name="BlamedLines">How many lines know their commit now.</param>
public sealed record BlameProgressed(long Generation, int BlamedLines) : BlameCommand;

/// <summary>Git finished the blame.</summary>
/// <param name="Generation">The read number.</param>
/// <param name="BlamedLines">How many lines know their commit.</param>
public sealed record BlameFinished(long Generation, int BlamedLines) : BlameCommand;

/// <summary>The read failed.</summary>
/// <param name="Generation">The read number.</param>
/// <param name="Text">What to tell the user.</param>
public sealed record BlameFailed(long Generation, string Text) : BlameCommand;

/// <summary>A search ended.</summary>
/// <param name="Generation">The read number of the searched lines.</param>
/// <param name="Query">The text that was searched.</param>
/// <param name="Index">The matching line, -1 when there is none.</param>
public sealed record BlameMatchFound(long Generation, string Query, int Index) : BlameCommand;
