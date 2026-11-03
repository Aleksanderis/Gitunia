using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Picks the frame to draw for the overlay state, or none.</summary>
public static class OverlayFrameFactory
{
    /// <param name="state">The overlay state.</param>
    /// <param name="helpTabs">The help content, used while the help is open.</param>
    /// <param name="pendingKeys">The unfinished chord, used for the which-key popup.</param>
    /// <param name="whichKeyRows">The keys that can follow the chord.</param>
    /// <param name="screenWidth">Terminal columns.</param>
    /// <param name="screenHeight">Terminal rows.</param>
    /// <param name="glyphs">The characters to draw rules with.</param>
    /// <param name="bannerShown">Whether the operation banner is drawn above the status bar.</param>
    public static OverlayFrame? Create(
        OverlayState state,
        IReadOnlyList<HelpTab> helpTabs,
        string pendingKeys,
        IReadOnlyList<WhichKeyRow> whichKeyRows,
        int screenWidth,
        int screenHeight,
        GraphGlyphSet glyphs,
        bool bannerShown = false)
    {
        return state.Kind switch
        {
            OverlayKind.Help when state.IsHelpFiltered => HelpFrameBuilder.Build(
                [HelpBuilder.Filter(helpTabs, state.HelpFilter)],
                0,
                state.HelpView,
                state.HelpScroll,
                screenWidth,
                screenHeight,
                glyphs,
                filter: state.HelpFilter,
                isEditingFilter: state.IsEditingHelpFilter,
                filterCaret: state.HelpFilterCaret),
            OverlayKind.Help => HelpFrameBuilder.Build(
                helpTabs,
                state.HelpTab,
                state.HelpView,
                state.HelpScroll,
                screenWidth,
                screenHeight,
                glyphs),
            OverlayKind.Messages => HelpFrameBuilder.Build(
                helpTabs,
                0,
                string.Empty,
                state.HelpScroll,
                screenWidth,
                screenHeight,
                glyphs,
                "Status messages"),
            OverlayKind.Output => HelpFrameBuilder.Build(
                helpTabs,
                0,
                string.Empty,
                state.HelpScroll,
                screenWidth,
                screenHeight,
                glyphs,
                state.HelpView),
            OverlayKind.Confirm when state.Confirm is { } prompt => ConfirmFrameBuilder.Build(prompt, screenWidth, screenHeight, glyphs),
            OverlayKind.Palette when state.Palette is { } palette => PaletteFrameBuilder.Build(palette, screenWidth, screenHeight, glyphs),
            OverlayKind.ContextMenu when state is { Palette: { } menu, MenuAnchor: { } anchor } =>
                ContextMenuFrameBuilder.Build(
                    menu,
                    anchor,
                    screenWidth,
                    screenHeight,
                    glyphs,
                    state.MenuTitle ?? ContextMenuFrameBuilder.DefaultTitle),
            _ when state.WhichKeyVisible => WhichKeyFrameBuilder.Build(
                pendingKeys,
                whichKeyRows,
                screenWidth,
                screenHeight,
                glyphs,
                bannerShown),
            _ => null,
        };
    }
}
