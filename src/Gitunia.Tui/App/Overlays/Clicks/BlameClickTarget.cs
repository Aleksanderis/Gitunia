using Gitunia.Tui.App.Blame;

namespace Gitunia.Tui.App.Overlays.Clicks;

/// <summary>A click on the blame view.</summary>
public sealed class BlameClickTarget(BlameInput blame) : IOverlayClickTarget
{
    /// <inheritdoc />
    public OverlayClick? ClickAt(int line, int column) =>
        OverlayClick.OfEditor(blame.OnLineClicked(line, column, out var handled), handled);
}
