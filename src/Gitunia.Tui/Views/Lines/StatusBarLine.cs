using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Views.Lines;

/// <summary>The status bar on the last row.</summary>
/// <param name="content">The screen model.</param>
/// <param name="geometry">Where the panes are.</param>
/// <param name="glyphs">The characters the bar draws with.</param>
public sealed class StatusBarLine(IScreenContent content, PaneGeometry geometry, GraphGlyphSet glyphs) : ILineSource
{
    /// <inheritdoc />
    public StyledLine Line(int width) =>
        ChromeBuilder.Status(BannerRenderer.ForStatusBar(content.Model(), geometry.ScreenHeight), width, glyphs);
}
