using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;
using Gitunia.Tui.Input.Drag;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Tabs;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>The tab row: draws the tab labels, reports which tab was clicked and which way a dragged tab chip moves.</summary>
public sealed class TabBarView(IScreenContent content, GraphGlyphSet glyphs) : View
{
    private readonly DragTracker _drag = new();
    private int? _pressedNumber;
    private int _draggedNumber;
    private int _netMoves;
    private bool _isDragging;

    /// <summary>Raised with the one-based tab number when a tab is clicked.</summary>
    public event Action<int>? TabClicked;

    /// <summary>Raised with the one-based tab number when the dirty marker of a tab is clicked.</summary>
    public event Action<int>? DirtyMarkerClicked;

    /// <summary>Raised when the commands button at the right end of the row is clicked.</summary>
    public event Action? CommandsClicked;

    /// <summary>Raised with the mouse view name of a fetch, pull, push or cancel chip that was clicked.</summary>
    public event Action<string>? RemoteChipClicked;

    /// <summary>Raised when the "+" button after the last tab is clicked.</summary>
    public event Action? NewTabClicked;

    /// <summary>
    /// Raised while a tab chip is dragged: <see cref="MouseGesture.DragBack"/> or <see cref="MouseGesture.DragForward"/> and
    /// the number of places the tab moves (always one while following the pointer, more when a cancel puts it back).
    /// </summary>
    public event Action<MouseGesture, int>? TabDragged;

    /// <summary>Cancels a drag in progress: the tab goes back to where the drag began. Returns false when none is going on.</summary>
    public bool CancelDrag()
    {
        if (!_isDragging)
        {
            return false;
        }

        UndoMoves();
        _isDragging = false;

        return true;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        LinePainter.Paint(this, ChromeBuilder.Tabs(content.Model(), Viewport.Width, glyphs), 0, 0, isFocused: false);
        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        var phase = mouse.Position is { } point ? _drag.Feed(mouse.Flags, point.X, point.Y) : DragPhase.None;

        // Also on the release of a plain click (no phase): the grab taken at the press must not outlive it.
        DragGrab.Update(this, mouse.Flags, phase);
        if (content.Model().Tabs is { Count: > 0 } && phase != DragPhase.None)
        {
            return HandleDrag(phase, mouse);
        }

        if (!MouseFlagTranslator.IsLeftClick(mouse.Flags) || mouse.Position is not { } position)
        {
            return base.OnMouseEvent(mouse);
        }

        // The button only exists when the row shows tabs.
        if (content.Model().Tabs is not { Count: > 0 } labels)
        {
            return base.OnMouseEvent(mouse);
        }

        if (TabBarLayout.IsOnCommands(Viewport.Width, position.X, content.Model().Keys))
        {
            CommandsClicked?.Invoke();
            return true;
        }

        if (content.Model().Picker is null
            && RemoteChips.ViewAt(Viewport.Width, content.Model().RemoteBusy, position.X, content.Model().Keys) is { } chipView)
        {
            RemoteChipClicked?.Invoke(chipView);
            return true;
        }

        var cells = TabBarLayout.Layout(labels, Viewport.Width, glyphs, content.Model().Keys);
        if (TabBarLayout.IsOnNewTab(cells, position.X))
        {
            NewTabClicked?.Invoke();
            return true;
        }

        if (TabBarLayout.DirtyMarkerAt(cells, position.X) is { } dirtyNumber)
        {
            DirtyMarkerClicked?.Invoke(dirtyNumber);
            return true;
        }

        // The row starts with "< " when tabs are cut off on the left; the layout already counts it in the cell columns.
        if (TabBarLayout.NumberAt(cells, position.X) is { } number)
        {
            TabClicked?.Invoke(number);
        }

        return true;
    }

    private bool HandleDrag(DragPhase phase, Mouse mouse)
    {
        switch (phase)
        {
            case DragPhase.Press:
                _pressedNumber = NumberAtPress();
                _isDragging = false;

                return base.OnMouseEvent(mouse);
            case DragPhase.Start when _pressedNumber is { } number:
                _isDragging = true;
                _draggedNumber = number;
                _netMoves = 0;

                // Dragging a chip makes its tab the active one, the tab the move actions act on.
                TabClicked?.Invoke(number);

                return true;
            case DragPhase.Move when _isDragging && mouse.Position is { } position:
                FollowPointer(position.X);

                return true;
            case DragPhase.End:
                FinishDrag(mouse);

                return true;
            case DragPhase.SwallowClick:
                return true;
            default:
                return base.OnMouseEvent(mouse);
        }
    }

    private int? NumberAtPress()
    {
        var labels = content.Model().Tabs;
        if (labels is not { Count: > 0 })
        {
            return null;
        }

        var cells = TabBarLayout.Layout(labels, Viewport.Width, glyphs, content.Model().Keys);

        return TabBarLayout.NumberAt(cells, _drag.Origin.X);
    }

    // A fast drag can skip columns, so the chip catches up with the pointer one place at a time (bounded by the tab count).
    private void FollowPointer(int column)
    {
        for (var attempt = 0; attempt < content.Model().TabCount + 1; attempt++)
        {
            if (!FollowPointerOnce(column))
            {
                return;
            }
        }
    }

    private bool FollowPointerOnce(int column)
    {
        var labels = content.Model().Tabs;
        if (labels is not { Count: > 0 })
        {
            return false;
        }

        var cells = TabBarLayout.Layout(labels, Viewport.Width, glyphs, content.Model().Keys);

        // The model is still the one before the last move: wait for the redraw instead of moving twice.
        if (cells.FirstOrDefault(cell => cell.IsActive)?.Number != _draggedNumber)
        {
            return false;
        }

        var step = TabDragPlanner.Step(cells, _draggedNumber, column);
        if (step == 0)
        {
            return false;
        }

        _draggedNumber += step;
        _netMoves += step;
        TabDragged?.Invoke(step < 0 ? MouseGesture.DragBack : MouseGesture.DragForward, 1);

        return true;
    }

    // Released outside the tab row: the drag is cancelled and the tab goes back.
    private void FinishDrag(Mouse mouse)
    {
        if (_isDragging && mouse.Position is { } position)
        {
            if (Viewport.Contains(position))
            {
                FollowPointer(position.X);
            }
            else
            {
                UndoMoves();
            }
        }

        _isDragging = false;
    }

    private void UndoMoves()
    {
        if (_netMoves != 0)
        {
            TabDragged?.Invoke(_netMoves < 0 ? MouseGesture.DragForward : MouseGesture.DragBack, Math.Abs(_netMoves));
        }

        _netMoves = 0;
    }
}
