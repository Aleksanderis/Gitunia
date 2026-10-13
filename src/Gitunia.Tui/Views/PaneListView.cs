using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views;

/// <summary>
/// A list pane that reports its mouse input as <see cref="MouseGesture"/>s instead of scrolling by itself: the toolkit
/// still selects the clicked row, but the wheel, the double-click and the drag are decided by the shared mouse map, so they run the same
/// actions as keys. A gesture first gives the pane the focus. A drag grabs the mouse (so it goes on outside the pane; resting outside auto-scrolls, see <see cref="DragAutoScroll"/>), selects the row it
/// started on and then reports how many rows the pointer is above or below the cursor.
/// </summary>
public sealed class PaneListView : ListView
{
    private readonly DragTracker drag = new();
    private int lastPointerRow;
    private int autoScrollGeneration;
    private bool isAutoScrolling;

    /// <summary>True while something modal owns the screen: mouse input is swallowed without focusing or selecting anything.</summary>
    public Func<bool> IsSuppressed { get; set; } = () => false;

    /// <summary>
    /// Raised after the pane took the focus and (for clicks) selected the row under the pointer. The number is the row distance for
    /// <see cref="MouseGesture.DragUp"/> and <see cref="MouseGesture.DragDown"/>, null for the other gestures.
    /// </summary>
    public event Action<MouseGesture, int?>? GestureRaised;

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

        var phase = mouse.Position is { } position ? drag.Feed(mouse.Flags, position.X, position.Y) : DragPhase.None;
        DragGrab.Update(this, mouse.Flags, phase);
        if (phase != DragPhase.None)
        {
            return HandleDrag(phase, mouse);
        }

        if (!MouseFlagTranslator.TryTranslate(mouse.Flags, out var gesture))
        {
            return base.OnMouseEvent(mouse);
        }

        FocusPane();
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
            return base.OnMouseEvent(mouse);
        }

        if (phase == DragPhase.Start)
        {
            FocusPane();
            SelectItem(drag.Origin.Y);
            GestureRaised?.Invoke(MouseGesture.DragStart, null);
        }

        if (phase is DragPhase.Start or DragPhase.Move && mouse.Position is { } position)
        {
            lastPointerRow = position.Y;
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
        if (isAutoScrolling || App is null)
        {
            return;
        }

        isAutoScrolling = true;
        var generation = ++autoScrollGeneration;
        App.AddTimeout(DragAutoScroll.Interval, () =>
        {
            var step = DragAutoScroll.Step(lastPointerRow, Viewport.Height);
            if (generation != autoScrollGeneration || !drag.IsPressed || step == 0 || IsSuppressed())
            {
                isAutoScrolling = false;
                return false;
            }

            var count = Source?.Count ?? 0;
            var target = Math.Clamp((SelectedItem ?? 0) + step, 0, Math.Max(count - 1, 0));
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
