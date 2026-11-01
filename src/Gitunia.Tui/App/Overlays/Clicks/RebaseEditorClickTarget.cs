using Gitunia.Core.Mouse;
using Gitunia.Tui.App.Rebase;

namespace Gitunia.Tui.App.Overlays.Clicks;

/// <summary>A click on the interactive rebase editor.</summary>
public sealed class RebaseEditorClickTarget(RebaseEditorInput editor) : IOverlayClickTarget
{
    /// <inheritdoc />
    public OverlayClick? ClickAt(int line, int column) =>
        OverlayClick.OfEditor(editor.OnLineClicked(line, column, out var handled), handled);
}
