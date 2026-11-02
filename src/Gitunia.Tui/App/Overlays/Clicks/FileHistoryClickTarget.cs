using Gitunia.Tui.App.FileHistory;

namespace Gitunia.Tui.App.Overlays.Clicks;

/// <summary>A click on the file history.</summary>
public sealed class FileHistoryClickTarget(FileHistoryInput history) : IOverlayClickTarget
{
    /// <inheritdoc />
    public OverlayClick? ClickAt(int line, int column) =>
        OverlayClick.OfEditor(history.OnLineClicked(line, out var handled), handled);
}
