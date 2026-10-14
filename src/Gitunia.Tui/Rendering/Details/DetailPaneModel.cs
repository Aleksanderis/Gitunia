namespace Gitunia.Tui.Rendering.Details;

/// <summary>The detail pane as the frame composer draws it.</summary>
/// <param name="Content">The rows to show.</param>
/// <param name="SelectedRow">The row under the cursor.</param>
/// <param name="TopRow">The first visible row.</param>
/// <param name="IsFocused">The pane has focus (the cursor row is highlighted and the title is marked).</param>
public sealed record DetailPaneModel(DetailContent Content, int SelectedRow, int TopRow, bool IsFocused);
