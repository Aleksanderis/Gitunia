using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;
using Gitunia.Tui.Input.Drag;
using Gitunia.Tui.Rendering.Styling;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views.Lists;

/// <summary>
/// A list pane that reports its mouse input as <see cref="MouseGesture"/>s instead of scrolling by itself: the toolkit
/// still selects the clicked row, but the wheel, the double-click and the drag are decided by the shared mouse map, so they run the same
/// actions as keys. A gesture first gives the pane the focus. A drag grabs the mouse (so it goes on outside the pane; resting outside
/// auto-scrolls, see <see cref="DragAutoScroll"/>), selects the row it started on and then reports how many rows the pointer is above or below the cursor.
/// </summary>
public sealed class PaneListView : ListView
{
    private readonly DragTracker _drag = new();
    private int _lastPointerRow;
    private bool _isAutoScrolling;

    /// <summary>
    /// The rows of this pane have an actions menu: the selected row shows a <c>...</c> marker
    /// at its right edge and a click on it opens the menu (#479).
    /// </summary>
    public bool OpensRowMenu { get; set; }

    /// <summary>
    /// A double-click on blank space below the last row is dropped: the double-click acts on the selected row
    /// (the graph opens its menu) and the pointer is not on it (#484).
    /// </summary>
    public bool IgnoresBlankDoubleClick { get; set; }

    /// <summary>
    /// A drag selects text in this pane (the message box): it is reported through <see cref="PointerDragged"/> with
    /// the pointer's cell instead of selecting rows or starting a Visual range.
    /// </summary>
    public bool ReportsPointerDrag { get; set; }

    /// <summary>True while something modal owns the screen: mouse input is swallowed without focusing or selecting anything.</summary>
    public IMouseGate Gate { get; init; } = new MouseGate();

    /// <summary>
    /// Raised after the pane took the focus and (for clicks) selected the row under the pointer. The number is the row distance for
    /// <see cref="MouseGesture.DragUp"/> and <see cref="MouseGesture.DragDown"/>, null for the other gestures.
    /// </summary>
    public event Action<MouseGesture, int?>? GestureRaised;

    /// <summary>Raised for a click, before <see cref="GestureRaised"/>, with the pointer's column and row inside the pane.</summary>
    public event Action<int, int>? ClickPositioned;

    /// <summary>
    /// Raised for a drag in a pane with <see cref="ReportsPointerDrag"/>: first the cell where the button went down
    /// (<c>true</c>), then every cell the pointer reaches (<c>false</c>); the cells are inside the pane or beyond its
    /// edges while the mouse is grabbed.
    /// </summary>
    public event Action<int, int, bool>? PointerDragged;

    /// <summary>
    /// Raised instead of <see cref="GestureRaised"/> when a click or double-click hit a button of an <see cref="IButtonSource"/> source (not a click on the row);
    /// the pane has the focus by then and the cursor has not moved.
    /// </summary>
    public event Action<string>? ButtonClicked;

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (Gate.IsSuppressed)
        {
            // Not acted on here (not even by the toolkit: no focus, no selection); the application may still use the wheel and the click for the overlay on top.
            if (MouseFlagTranslator.TryTranslate(mouse.Flags, out var suppressedGesture))
            {
                GestureRaised?.Invoke(suppressedGesture, null);
            }

            return true;
        }

        var phase = mouse.Position is { } position ? _drag.Feed(mouse.Flags, position.X, position.Y) : DragPhase.None;
        DragGrab.Update(this, mouse.Flags, phase);
        if (phase != DragPhase.None)
        {
            return HandleDrag(phase, mouse);
        }

        if (!MouseFlagTranslator.TryTranslate(mouse.Flags, out var gesture))
        {
            return base.OnMouseEvent(mouse);
        }

        // A file row's double-click is the row's own action (open the diff, stage): only a single click hits its marker.
        var hitsMarker = gesture == MouseGesture.Click
            || (gesture == MouseGesture.DoubleClick && Source is not IRowMenuSource);
        if (hitsMarker
            && mouse.Position is { } at
            && IsOnRowMenuMarker(at.X, at.Y))
        {
            FocusPane();
            ButtonClicked?.Invoke(DefaultMouseMap.RowMenuButton);
            return true;
        }

        if (gesture is MouseGesture.Click or MouseGesture.DoubleClick
            && mouse.Position is { } buttonAt
            && Source is IButtonSource buttons
            && buttons.ButtonAt(this, buttonAt.X, buttonAt.Y) is { } buttonView)
        {
            FocusPane();
            ButtonClicked?.Invoke(buttonView);
            return true;
        }

        FocusPane();
        if (gesture == MouseGesture.DoubleClick
            && IgnoresBlankDoubleClick
            && mouse.Position is { } blank
            && IsBelowLastRow(blank.Y))
        {
            return true;
        }

        if (gesture == MouseGesture.Click && mouse.Position is { } clicked)
        {
            ClickPositioned?.Invoke(clicked.X, clicked.Y);
        }

        if (gesture is MouseGesture.Click or MouseGesture.DoubleClick)
        {
            base.OnMouseEvent(mouse);
        }

        GestureRaised?.Invoke(gesture, null);
        return true;
    }

    private bool HandleDrag(DragPhase phase, Mouse mouse)
    {
        if (phase == DragPhase.SwallowClick)
        {
            return true;
        }

        if (phase == DragPhase.Press)
        {
            _isAutoScrolling = false;
            return base.OnMouseEvent(mouse);
        }

        if (ReportsPointerDrag && phase is DragPhase.Start or DragPhase.Move && mouse.Position is { } dragged)
        {
            if (phase == DragPhase.Start)
            {
                FocusPane();
                PointerDragged?.Invoke(_drag.Origin.X, _drag.Origin.Y, true);
            }

            PointerDragged?.Invoke(dragged.X, dragged.Y, false);

            return true;
        }

        if (phase == DragPhase.Start)
        {
            FocusPane();
            SelectItem(_drag.Origin.Y);
            GestureRaised?.Invoke(MouseGesture.DragStart, null);
        }

        if (phase is DragPhase.Start or DragPhase.Move && mouse.Position is { } position)
        {
            _lastPointerRow = position.Y;
            var visibleRows = Viewport.Height;
            if (DragAutoScroll.Step(position.Y, visibleRows) == 0)
            {
                ExtendTo(position.Y);
            }
            else
            {
                StartAutoScroll();
            }
        }

        return true;
    }

    // While the pointer rests above or below the pane no events arrive, so a timer keeps moving the cursor until it is back inside or released.
    private void StartAutoScroll()
    {
        if (_isAutoScrolling || App is null)
        {
            return;
        }

        _isAutoScrolling = true;
        App.AddTimeout(DragAutoScroll.Interval, () =>
        {
            var step = DragAutoScroll.Step(_lastPointerRow, Viewport.Height);
            if (!_drag.IsPressed || step == 0 || Gate.IsSuppressed)
            {
                _isAutoScrolling = false;
                return false;
            }

            var count = Source?.Count ?? 0;
            var target = Math.Clamp((SelectedItem ?? 0) + step, 0, Math.Max(count - 1, 0));
            if (target == (SelectedItem ?? 0))
            {
                _isAutoScrolling = false;
                return false;
            }

            ExtendTo(target - Viewport.Y);
            return true;
        });
    }

    // The pointer's row may be above or below the pane while the mouse is grabbed; the list clamps it.
    private void ExtendTo(int pointerRow)
    {
        var count = Source?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        var target = Math.Clamp(Viewport.Y + pointerRow, 0, count - 1);
        var delta = target - (SelectedItem ?? 0);
        if (delta != 0)
        {
            GestureRaised?.Invoke(delta < 0 ? MouseGesture.DragUp : MouseGesture.DragDown, Math.Abs(delta));
        }
    }

    // The marker sits on the selected row only (#479); a click on it is a button click, the cursor does not move.
    private bool IsOnRowMenuMarker(int column, int row) =>
        OpensRowMenu
        && SelectedItem is { } selected
        && selected < (Source?.Count ?? 0)
        && (Source is not IRowMenuSource menus || menus.HasRowMenu(selected))
        && RowMenuMarker.IsHit(Viewport.Width, column, Viewport.Y + row == selected);

    // Blank space under the last row (#484): a double-click there must not open the menu of the selected row.
    private bool IsBelowLastRow(int row) => Viewport.Y + row >= (Source?.Count ?? 0);

    private void SelectItem(int pointerRow)
    {
        var count = Source?.Count ?? 0;
        if (count > 0)
        {
            SelectedItem = Math.Clamp(Viewport.Y + pointerRow, 0, count - 1);
        }
    }

    private void FocusPane()
    {
        if (!HasFocus)
        {
            SetFocus();
        }
    }
}
