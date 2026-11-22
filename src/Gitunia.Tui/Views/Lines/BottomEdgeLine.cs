using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Views.Lines;

/// <summary>The bottom edge of the frame, which stays in place while the operation banner has its own row below it.</summary>
/// <param name="parts">What the chrome is drawn from.</param>
public sealed class BottomEdgeLine(ChromeParts parts) : ILineSource
{
    /// <inheritdoc />
    public StyledLine Line(int width) =>
        parts.Grids.Current().Row(ScreenLayout.FrameBottom(parts.Geometry.FrameHeight), 0, width);
}
