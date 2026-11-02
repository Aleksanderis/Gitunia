using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// The mouse on an open overlay: a click goes to the overlay under it (by the router), a double-click and a drag only
/// mean something in the conflict editor, whose version rows start a Visual selection.
/// </summary>
/// <param name="mouse">Runs the action a gesture is bound to.</param>
/// <param name="clicks">Finds the overlay under a click.</param>
/// <param name="guard">Ignores a double-click that follows a click on an overlay.</param>
/// <param name="editors">The two editors; the conflict editor takes the drag and the double-click.</param>
/// <param name="cover">Which overlays cover the conflict editor.</param>
public sealed class OverlayGestures(
    MouseController mouse,
    OverlayClickRouter clicks,
    OverlayClickGuard guard,
    EditorOverlays editors,
    OverlayCover cover)
{
    /// <summary>
    /// A click on an overlay line goes to the first overlay under it; a click on a palette row highlights it and runs it
    /// (the palette is the menu of every action).
    /// </summary>
    /// <param name="line">The frame line.</param>
    /// <param name="column">The frame column.</param>
    public void OnLineClicked(int line, int column)
    {
        if (clicks.ClickAt(line, column) is not { } click)
        {
            return;
        }

        if (click.GuardsDoubleClick)
        {
            guard.Mark();
        }

        if (click.View is not null)
        {
            mouse.Handle(click.View, MouseGesture.Click, click.Target);
        }
    }

    /// <summary>
    /// Only the conflict editor uses the double-click of an overlay (a version row takes its version; a chip already ran on
    /// the first click and does nothing again); the other overlays keep ignoring it.
    /// </summary>
    /// <param name="line">The frame line.</param>
    /// <param name="column">The frame column.</param>
    public void OnLineDoubleClicked(int line, int column)
    {
        if (!editors.Conflict.IsOpen)
        {
            return;
        }

        var view = editors.Conflict.OnLineClicked(
            cover.IsOtherThanConflictEditorOpen(),
            line,
            column,
            out var handled,
            isDoubleClick: true);
        if (!handled)
        {
            return;
        }

        guard.Mark();
        if (view is not null)
        {
            mouse.Handle(view, MouseGesture.Click);
        }
    }

    /// <summary>A drag began on an overlay: only the conflict editor uses it, from a version row it starts a Visual selection.</summary>
    /// <param name="line">The frame line.</param>
    /// <param name="column">The frame column.</param>
    public void OnDragStarted(int line, int column)
    {
        if (editors.Conflict.OnDragStarted(cover.IsOtherThanConflictEditorOpen(), line, column) is { } view)
        {
            mouse.Handle(view, MouseGesture.DragStart);
        }
    }

    /// <summary>The pointer of a drag over an overlay moved to a frame line.</summary>
    /// <param name="line">The frame line.</param>
    public void OnDragged(int line) => editors.Conflict.OnDragged(cover.IsOtherThanConflictEditorOpen(), line);
}
