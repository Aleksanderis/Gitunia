using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Views.Lines;

/// <summary>The operation banner, drawn on its own row between the frame and the status bar.</summary>
/// <param name="parts">What the chrome is drawn from.</param>
public sealed class OperationBannerLine(ChromeParts parts) : ILineSource
{
    /// <inheritdoc />
    public StyledLine Line(int width)
    {
        var model = parts.Content.Model();

        return model.Banner is null
            ? new LineBuilder(width, parts.Panes.Content.Glyphs.Ellipsis).Build(SegmentRole.Status)
            : BannerRenderer.RowLine(model, width, parts.Panes.Content.Glyphs);
    }
}
