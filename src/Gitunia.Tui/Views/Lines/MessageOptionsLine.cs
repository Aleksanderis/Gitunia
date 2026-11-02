using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;

namespace Gitunia.Tui.Views.Lines;

/// <summary>The option row under the commit message box.</summary>
/// <param name="parts">What the chrome is drawn from.</param>
public sealed class MessageOptionsLine(ChromeParts parts) : ILineSource
{
    /// <inheritdoc />
    public StyledLine Line(int width) =>
        MessageBoxRenderer.OptionsLine(
            parts.Panes.Content.MessageBox.Draft,
            width,
            PanePadding.ColumnsFor(parts.Geometry.ScreenWidth),
            parts.Panes.Content.Glyphs,
            parts.Panes.Content.Keys,
            parts.Panes.Content.MessageBox.IsCommitting);
}
