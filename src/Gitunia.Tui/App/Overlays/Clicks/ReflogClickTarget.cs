using Gitunia.Tui.App.Reflog;

namespace Gitunia.Tui.App.Overlays.Clicks;

/// <summary>A click on the reflog list.</summary>
public sealed class ReflogClickTarget(ReflogInput reflog) : IOverlayClickTarget
{
    /// <inheritdoc />
    public OverlayClick? ClickAt(int line, int column) =>
        OverlayClick.OfEditor(reflog.OnLineClicked(line, out var handled), handled);
}
