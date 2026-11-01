using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tabs;

/// <summary>What the detail pane showed before and shows now, for deciding where its cursor goes.</summary>
/// <param name="PreviousShortHash">The commit the pane showed before.</param>
/// <param name="ShortHash">The commit the pane shows now.</param>
/// <param name="PreviousTab">The tab the pane showed before.</param>
/// <param name="Tab">The tab the pane shows now.</param>
/// <param name="PreviousFileCount">The number of file rows the pane listed before.</param>
/// <param name="Landing">The row a fresh view of the content starts on (the first file on the Changes tab).</param>
/// <param name="CurrentRow">The row the cursor is on now, null when there is none.</param>
/// <param name="RowCount">The number of rows now.</param>
public sealed record DetailCursorRequest(
    string PreviousShortHash,
    string ShortHash,
    BottomTab PreviousTab,
    BottomTab Tab,
    int PreviousFileCount,
    int Landing,
    int? CurrentRow,
    int RowCount);
