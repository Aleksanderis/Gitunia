using Gitunia.Core.Diff;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>The diff pane as the frame composer draws it.</summary>
/// <param name="Document">The rows to show.</param>
/// <param name="SelectedRow">The row under the cursor.</param>
/// <param name="TopRow">The first visible row.</param>
/// <param name="IsFocused">The pane has focus.</param>
/// <param name="Highlight">The diff search text to highlight, null for none.</param>
/// <param name="IsWrapped">Word wrap is on (shown in the title).</param>
/// <param name="OptionsLabel">The non-default diff options as a short title marker, empty for the defaults.</param>
/// <param name="FileNumber">Position (1-based) of the shown file among the files of the commit, 0 for none.</param>
/// <param name="FileCount">Files of the commit, 0 when the pane does not show a commit's files; in the folder view the
/// files of the folder.</param>
/// <param name="IsFolder">The pane shows the files of a folder (tree style of the Changes tab).</param>
public sealed record DiffPaneModel(
    DiffDocument Document,
    int SelectedRow,
    int TopRow,
    bool IsFocused,
    string? Highlight = null,
    bool IsWrapped = false,
    string OptionsLabel = "",
    int FileNumber = 0,
    int FileCount = 0,
    bool IsFolder = false);
