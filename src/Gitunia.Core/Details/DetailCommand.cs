using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Details;

/// <summary>A key of the bottom detail pane (its tabs, the path style, the file trees) for one tab. Dropped when the tab
/// is no longer open.</summary>
public abstract record DetailCommand : AppCommand
{
    private protected DetailCommand(TabId tabId) => TabId = tabId;

    /// <summary>The tab the command is for.</summary>
    public TabId TabId { get; }
}

/// <summary>Switch the tab of the bottom detail pane.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Move">Where to.</param>
public sealed record DetailTabMoved(TabId TabId, DetailTabMove Move) : DetailCommand(TabId);

/// <summary>Set the path style of the file lists, or step through the styles.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Style">The style, null to step to the next one.</param>
public sealed record PathStyleChosen(TabId TabId, FilePathStyle? Style) : DetailCommand(TabId);

/// <summary>Open the filter box of the Files tab.</summary>
/// <param name="TabId">The tab.</param>
public sealed record FileFilterStarted(TabId TabId) : DetailCommand(TabId);

/// <summary>A key typed into the filter box.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Key">The key.</param>
public sealed record FileFilterKeyTyped(TabId TabId, KeyEvent Key) : DetailCommand(TabId);

/// <summary>Close the filter box and keep the filter.</summary>
/// <param name="TabId">The tab.</param>
public sealed record FileFilterAccepted(TabId TabId) : DetailCommand(TabId);

/// <summary>Close the filter box and clear the filter.</summary>
/// <param name="TabId">The tab.</param>
public sealed record FileFilterCancelled(TabId TabId) : DetailCommand(TabId);

/// <summary>Clear a kept filter (back in normal mode).</summary>
/// <param name="TabId">The tab.</param>
public sealed record FileFilterCleared(TabId TabId) : DetailCommand(TabId);

/// <summary>Enter on a row of the Files tab: open or close a directory, show a file's content.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Cursor">The row under the cursor.</param>
public sealed record FileTreeOpened(TabId TabId, FileTreeCursor Cursor) : DetailCommand(TabId);

/// <summary>Open the directory under the cursor of the Files tab.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Cursor">The row under the cursor.</param>
public sealed record FileTreeExpanded(TabId TabId, FileTreeCursor Cursor) : DetailCommand(TabId);

/// <summary>Close the directory under the cursor of the Files tab, or climb to its parent.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Cursor">The row under the cursor.</param>
public sealed record FileTreeCollapsed(TabId TabId, FileTreeCursor Cursor) : DetailCommand(TabId);

/// <summary>Open or close the folder under the cursor of the Changes tab's tree style.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Cursor">The row under the cursor.</param>
public sealed record ChangesFolderToggled(TabId TabId, ChangesCursor Cursor) : DetailCommand(TabId);

/// <summary>Open the closed folder under the cursor of the Changes tab's tree style.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Cursor">The row under the cursor.</param>
public sealed record ChangesFolderExpanded(TabId TabId, ChangesCursor Cursor) : DetailCommand(TabId);

/// <summary>Close the folder under the cursor of the Changes tab, or climb to its parent folder.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Cursor">The row under the cursor.</param>
public sealed record ChangesFolderCollapsed(TabId TabId, ChangesCursor Cursor) : DetailCommand(TabId);
