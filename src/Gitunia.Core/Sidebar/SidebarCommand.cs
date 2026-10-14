using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Sidebar;

/// <summary>A change to the sidebar of one tab; routed like any <see cref="RepoCommand"/>.</summary>
public abstract record SidebarCommand : RepositoryCommand;

/// <summary>Move the cursor with a list motion (<c>j</c>, <c>gg</c>, <c>5G</c>, <c>Ctrl-d</c> ...); other actions are ignored.</summary>
/// <param name="Action">The bound list action.</param>
/// <param name="Count">The numeric prefix, or null.</param>
/// <param name="PageHeight">Visible rows, for the half-page motions.</param>
public sealed record MoveSidebarCursorCommand(ActionId Action, int? Count, int PageHeight) : SidebarCommand;

/// <summary>Put the cursor on a row (mouse click).</summary>
/// <param name="Index">The row index.</param>
public sealed record SetSidebarCursorCommand(int Index) : SidebarCommand;

/// <summary>Unfold the section under the cursor, or step into it; on an entry, move focus to the graph.</summary>
public sealed record ExpandSidebarCommand : SidebarCommand;

/// <summary>Fold the section under the cursor; on an entry, move the cursor to its section header.</summary>
public sealed record CollapseSidebarCommand : SidebarCommand;

/// <summary>Fold or unfold the section under the cursor.</summary>
public sealed record ToggleSidebarSectionCommand : SidebarCommand;

/// <summary>Set the name filter and put the cursor on the first match.</summary>
/// <param name="Text">The filter; empty clears it.</param>
public sealed record SetSidebarFilterCommand(string Text) : SidebarCommand;
