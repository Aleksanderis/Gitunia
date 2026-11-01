using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Overlays;

/// <summary>What a mouse click on a line of the overlay frame means for the help, the palette, the menu and the confirm dialog.</summary>
/// <param name="store">The overlay state a click changes.</param>
/// <param name="overlays">Draws the frame the line belongs to.</param>
/// <param name="chrome">The terminal size.</param>
public sealed class OverlayHitTests(OverlayStore store, OverlayController overlays, IPaneChrome chrome)
{
    /// <summary>The answer drawn on a frame line of the open confirm dialog (a mouse click); null when the line is none or no dialog is open.</summary>
    /// <param name="frameLine">The line of the frame.</param>
    public ConfirmAnswer? ConfirmAnswerAt(int frameLine)
    {
        if (store.State.Kind == OverlayKind.Confirm && overlays.CurrentFrame() is { } frame)
        {
            return ConfirmFrameBuilder.AnswerAt(frameLine, frame.Height);
        }

        return null;
    }

    /// <summary>Highlights the palette entry drawn on a line of the overlay frame (a mouse click); false when the line is not an entry.</summary>
    /// <param name="frameLine">The line of the frame.</param>
    public bool SelectPaletteEntryAt(int frameLine)
    {
        if (store.State is not { Kind: OverlayKind.Palette, Palette: { } palette })
        {
            return false;
        }

        if (PaletteFrameBuilder.EntryIndexAt(palette, frameLine, chrome.ScreenSize.Height) is not { } index)
        {
            return false;
        }

        store.State = store.State.WithPalette(palette with { SelectedIndex = index });
        store.RaiseChanged();

        return true;
    }

    /// <summary>Highlights the menu entry drawn on a frame line (a click); false for any other line.</summary>
    /// <param name="frameLine">The line of the frame.</param>
    public bool SelectMenuEntryAt(int frameLine)
    {
        if (store.State is not { Kind: OverlayKind.ContextMenu, Palette: { } menu })
        {
            return false;
        }

        if (ContextMenuFrameBuilder.EntryIndexAt(menu, frameLine, chrome.ScreenSize.Height) is not { } index)
        {
            return false;
        }

        store.State = store.State.WithPalette(menu with { SelectedIndex = index });
        store.RaiseChanged();

        return true;
    }

    /// <summary>
    /// The help tab under a click on the overlay line <paramref name="frameLine"/> at <paramref name="frameColumn"/>
    /// (both inside the frame), as the number the click action takes (from 1), or null when it is not on a tab label.
    /// </summary>
    /// <param name="frameLine">The line of the frame.</param>
    /// <param name="frameColumn">The column of the frame.</param>
    public int? HelpTabNumberAt(int frameLine, int frameColumn)
    {
        var state = store.State;
        var size = chrome.ScreenSize;
        if (state.Kind == OverlayKind.Help
            && !state.IsHelpFiltered
            && HelpFrameBuilder.TabAt(store.HelpTabs, state.HelpTab, size.Width, size.Height, frameLine, frameColumn)
                is { } index)
        {
            return index + 1;
        }

        return null;
    }
}
