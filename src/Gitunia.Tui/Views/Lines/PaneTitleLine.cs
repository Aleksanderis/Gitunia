using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Views.Lines;

/// <summary>The title row of the detail or diff pane, with the frame rules under it resolved.</summary>
/// <param name="parts">What the chrome is drawn from.</param>
/// <param name="kind">Which title row this is.</param>
public sealed class PaneTitleLine(ChromeParts parts, PaneTitleKind kind) : ILineSource
{
    /// <summary>Where the title row is.</summary>
    public PaneRect Rect() =>
        kind == PaneTitleKind.Detail ? parts.Geometry.Rects().DetailTitle : parts.Geometry.Rects().DiffTitle;

    /// <inheritdoc />
    public StyledLine Line(int width)
    {
        var rect = Rect();
        var line = kind == PaneTitleKind.Detail ? parts.Content.DetailTitle(width) : parts.Content.DiffTitle(width);

        return parts.Grids.Current().ResolveRules(line, rect.Y, rect.X);
    }
}
