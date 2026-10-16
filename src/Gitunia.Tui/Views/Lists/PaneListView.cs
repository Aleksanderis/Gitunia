using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;
using Gitunia.Tui.Input.Drag;
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

    /// <summary>True while something modal owns the screen: mouse input is swallowed without focusing or selecting anything.</summary>
    public Func<bool> IsSuppressed { get; set; } = () => false;

    /// <summary>
    /// Raised after the pane took the focus and (for clicks) selected the row under the pointer. The number is the row distance for
    /// <see cref="MouseGesture.DragUp"/> and <see cref="MouseGesture.DragDown"/>, null for the other gestures.
    /// </summary>
    public event Action<MouseGesture, int?>? GestureRaised;

    /// <summary>Raised for a click, before <see cref="GestureRaised"/>, with the pointer's column and row inside the pane.</summary>
    public event Action<int, int>? ClickPositioned;

    /// <summary>Tells which button, if any, is drawn inside the list at a column and row of the pane; a click on one is a button click, not a click on the row.</summary>
    public Func<int, int, string?>? ButtonAt { get; set; }

    /// <summary>Raised instead of <see cref="GestureRaised"/> when a click or double-click hit a button from <see cref="ButtonAt"/>; the pane has the focus by then.</summary>
    public event Action<string>? ButtonClicked;

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (IsSuppressed())
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

        if (gesture is MouseGesture.Click or MouseGesture.DoubleClick
            && mouse.Position is { } at
            && ButtonAt?.Invoke(at.X, at.Y) is { } buttonView)
        {
            FocusPane();
            ButtonClicked?.Invoke(buttonView);
            return true;
        }

        FocusPane();
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
            if (!_drag.IsPressed || step == 0 || IsSuppressed())
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
