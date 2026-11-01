using Gitunia.Core.Keymap;

namespace Gitunia.Tui.App.Overlays.Clicks;

/// <summary>A click on an entry of the actions menu.</summary>
public sealed class ContextMenuClickTarget(OverlayHitTests hits) : IOverlayClickTarget
{
    /// <inheritdoc />
    public OverlayClick? ClickAt(int line, int column) =>
        hits.SelectMenuEntryAt(line) ? new OverlayClick(DefaultKeymap.ContextMenuView) : null;
}
