using Gitunia.Core.Diff;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>The diff pane as the frame composer draws it.</summary>
/// <param name="Document">The rows to show.</param>
/// <param name="ShortHash">The short hash of the commit, empty when none.</param>
/// <param name="SelectedRow">The row under the cursor.</param>
/// <param name="TopRow">The first visible row.</param>
/// <param name="IsFocused">The pane has focus.</param>
/// <param name="Highlight">The diff search text to highlight, null for none.</param>
/// <param name="IsWrapped">Word wrap is on (shown in the title).</param>
public sealed record DiffPaneModel(DiffDocument Document, string ShortHash, int SelectedRow, int TopRow, bool IsFocused, string? Highlight = null, bool IsWrapped = false);
