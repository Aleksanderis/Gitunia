using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Views.Lines;

/// <summary>The bottom edge of the frame, which carries the banner.</summary>
/// <param name="parts">What the chrome is drawn from.</param>
public sealed class BottomEdgeLine(ChromeParts parts) : ILineSource
{
    /// <inheritdoc />
    public StyledLine Line(int width)
    {
        var geometry = parts.Geometry;
        var edge = parts.Grids.Current().Row(ScreenLayout.FrameBottom(geometry.ScreenHeight), 0, width);

        return BannerRenderer.EdgeLine(
            parts.Content.Model(),
            edge,
            width,
            geometry.ScreenHeight,
            parts.Panes.Content.Glyphs);
    }
}
