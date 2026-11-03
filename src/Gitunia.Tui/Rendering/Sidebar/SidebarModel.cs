using System.Collections.Immutable;
using Gitunia.Core.Editing;
using Gitunia.Core.Sidebar;

namespace Gitunia.Tui.Rendering.Sidebar;

/// <summary>Everything needed to draw the sidebar: the rows and where the user is in them. Plain data, drawn without a terminal.</summary>
/// <param name="Rows">The visible rows.</param>
/// <param name="Cursor">Index of the selected row.</param>
/// <param name="TopRow">Index of the first visible row.</param>
/// <param name="FilterText">The name filter, empty when none.</param>
/// <param name="IsEditingFilter">The filter is being typed.</param>
/// <param name="IsFocused">The sidebar has focus.</param>
/// <param name="FilterCaret">The caret column in the filter (see <see cref="PromptCaret"/>).</param>
public sealed record SidebarModel(
    ImmutableArray<SidebarRow> Rows,
    int Cursor,
    int TopRow,
    string FilterText,
    bool IsEditingFilter,
    bool IsFocused,
    int FilterCaret = PromptCaret.End);
