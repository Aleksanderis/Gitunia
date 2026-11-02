using Gitunia.Tui.Rendering.Graph;

namespace Gitunia.Tui.Rendering.Sidebar;

/// <summary>The characters the sidebar is drawn with; there is a Unicode set and an ASCII fallback.</summary>
/// <param name="Expanded">Before the title of an unfolded section.</param>
/// <param name="Collapsed">Before the title of a folded section.</param>
/// <param name="Ahead">Before the number of commits ahead of the upstream.</param>
/// <param name="Behind">Before the number of commits behind the upstream.</param>
/// <param name="UpstreamGone">After a local branch whose upstream no longer exists.</param>
/// <param name="Divider">The vertical line between the sidebar and the graph.</param>
public sealed record SidebarGlyphs(
    char Expanded,
    char Collapsed,
    string Ahead,
    string Behind,
    string UpstreamGone,
    // ReSharper disable once NotAccessedPositionalProperty.Global : Not drawn yet; part of the full set
    char Divider)
{
    public static SidebarGlyphs Unicode { get; } = new('▾', '▸', "↑", "↓", "✗", '│');

    public static SidebarGlyphs Ascii { get; } = new('v', '>', "+", "-", "(x)", '|');

    /// <summary>The set that matches the graph glyphs in use.</summary>
    public static SidebarGlyphs For(GraphGlyphSet glyphs) => glyphs.IsAscii ? Ascii : Unicode;
}
