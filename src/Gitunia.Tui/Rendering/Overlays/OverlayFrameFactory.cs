using Gitunia.Core.Overlays;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Picks the frame to draw for the overlay state, or none.</summary>
public static class OverlayFrameFactory
{
    /// <param name="state">The overlay state.</param>
    /// <param name="helpSections">The help content, used while the help is open.</param>
    /// <param name="pendingKeys">The unfinished chord, used for the which-key popup.</param>
    /// <param name="whichKeyRows">The keys that can follow the chord.</param>
    /// <param name="screenWidth">Terminal columns.</param>
    /// <param name="screenHeight">Terminal rows.</param>
    /// <param name="glyphs">The characters to draw rules with.</param>
    public static OverlayFrame? Create(
        OverlayState state,
        IReadOnlyList<HelpSection> helpSections,
        string pendingKeys,
        IReadOnlyList<WhichKeyRow> whichKeyRows,
        int screenWidth,
        int screenHeight,
        GraphGlyphSet glyphs)
    {
        return state.Kind switch
        {
            OverlayKind.Help => HelpFrameBuilder.Build(helpSections, state.HelpView, state.HelpScroll, screenWidth, screenHeight, glyphs),
            OverlayKind.Messages => HelpFrameBuilder.Build(helpSections, string.Empty, state.HelpScroll, screenWidth, screenHeight, glyphs, "Status messages"),
            OverlayKind.Palette when state.Palette is { } palette => PaletteFrameBuilder.Build(palette, screenWidth, screenHeight, glyphs),
            _ when state.WhichKeyVisible => WhichKeyFrameBuilder.Build(pendingKeys, whichKeyRows, screenWidth, screenHeight, glyphs),
            _ => null,
        };
    }
}
