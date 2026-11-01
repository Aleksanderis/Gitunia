using Gitunia.Core.Mouse;
using Gitunia.Tui.App.Conflicts;

namespace Gitunia.Tui.App.Overlays.Clicks;

/// <summary>A click on the conflict editor.</summary>
public sealed class ConflictEditorClickTarget(ConflictEditorInput editor, OverlayCover cover) : IOverlayClickTarget
{
    /// <inheritdoc />
    public OverlayClick? ClickAt(int line, int column) =>
        OverlayClick.OfEditor(
            editor.OnLineClicked(cover.IsOtherThanConflictEditorOpen(), line, column, out var handled),
            handled);
}
