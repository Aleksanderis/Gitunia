using Gitunia.Core.Mouse;
using Gitunia.Core.Keymap;

namespace Gitunia.Tui.App.Overlays.Clicks;

/// <summary>A click on a palette row highlights it and runs it (the palette is the menu of every action).</summary>
public sealed class PaletteClickTarget(OverlayHitTests hits) : IOverlayClickTarget
{
    /// <inheritdoc />
    public OverlayClick? ClickAt(int line, int column) =>
        hits.SelectPaletteEntryAt(line) ? new OverlayClick(DefaultKeymap.PaletteView) : null;
}
