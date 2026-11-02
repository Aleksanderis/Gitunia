using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Views.Lines;

/// <summary>The filter row above the sidebar list.</summary>
/// <param name="content">The screen model and the pane titles.</param>
/// <param name="glyphs">The characters the row draws with.</param>
public sealed class SidebarFilterLine(IScreenContent content, GraphGlyphSet glyphs) : ILineSource
{
    /// <inheritdoc />
    public StyledLine Line(int width)
    {
        var chrome = content.SidebarFilter();
        var line = SidebarRowRenderer.FilterLine(
            chrome.FilterText,
            chrome.IsEditingFilter,
            PanePadding.TextWidth(width),
            glyphs);

        return PanePadding.Wrap(line);
    }
}
