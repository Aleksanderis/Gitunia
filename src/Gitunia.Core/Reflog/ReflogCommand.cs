using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Reflog.Models;

namespace Gitunia.Core.Reflog;

/// <summary>A command on the reflog overlay: user intent and the results of its effects.</summary>
public abstract record ReflogCommand : AppCommand;

/// <summary>Open the list for the active tab.</summary>
/// <param name="Branch">The local branch under the sidebar cursor, null for <c>HEAD</c>.</param>
public sealed record ReflogOpenRequested(string? Branch) : ReflogCommand;

/// <summary>A bound action while the list is open (it consumes every action).</summary>
/// <param name="Action">The action.</param>
/// <param name="Count">The numeric prefix, null when none was typed.</param>
/// <param name="PageRows">How many entry rows the list shows, for page motions.</param>
public sealed record ReflogActionPressed(ActionId Action, int? Count, int PageRows) : ReflogCommand;

/// <summary>A key typed into the search box or the ref picker.</summary>
/// <param name="Key">The key.</param>
public sealed record ReflogKeyTyped(KeyEvent Key) : ReflogCommand;

/// <summary>A click on an entry row.</summary>
/// <param name="Index">The entry.</param>
public sealed record ReflogEntryClicked(int Index) : ReflogCommand;

/// <summary>A click on a ref picker row.</summary>
/// <param name="Index">The row in the candidates.</param>
public sealed record ReflogPickerRowClicked(int Index) : ReflogCommand;

/// <summary>The tab of the list was closed: the list closes.</summary>
public sealed record ReflogTabClosed : ReflogCommand;

/// <summary>A read finished.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Generation">The read number of the effect.</param>
/// <param name="Reference">The ref as git was asked.</param>
/// <param name="Title">The ref as the user sees it.</param>
/// <param name="Refresh">True when the open list is only updated.</param>
/// <param name="Entries">The entries, newest first.</param>
public sealed record ReflogLoaded(
    TabId TabId,
    long Generation,
    string Reference,
    string Title,
    bool Refresh,
    IReadOnlyList<ReflogEntry> Entries) : ReflogCommand;

/// <summary>A read failed.</summary>
/// <param name="Text">What to tell the user.</param>
public sealed record ReflogReadFailed(string Text) : ReflogCommand;

/// <summary>A read was cancelled (its tab closed).</summary>
public sealed record ReflogReadCancelled : ReflogCommand;

/// <summary>The flow of an entry started: the list closes.</summary>
public sealed record ReflogEntryActionStarted : ReflogCommand;

/// <summary>The flow of an entry refused to start: the list stays and the status says why.</summary>
/// <param name="Text">The reason.</param>
public sealed record ReflogEntryActionRefused(string Text) : ReflogCommand;
