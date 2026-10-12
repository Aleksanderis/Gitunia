using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Views;

/// <summary>
/// A list pane that reports its mouse input as <see cref="MouseGesture"/>s instead of scrolling by itself: the toolkit
/// still selects the clicked row, but the wheel and the double-click are decided by the shared mouse map, so they run the same
/// actions as keys. A gesture first gives the pane the focus.
/// </summary>
public sealed class PaneListView : ListView
{
    /// <summary>True while something modal owns the screen: mouse input is swallowed without focusing or selecting anything.</summary>
    public Func<bool> IsSuppressed { get; set; } = () => false;

    /// <summary>Raised after the pane took the focus and (for clicks) selected the row under the pointer.</summary>
    public event Action<MouseGesture>? GestureRaised;

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (!MouseFlagTranslator.TryTranslate(mouse.Flags, out var gesture))
        {
            return base.OnMouseEvent(mouse);
        }

        if (IsSuppressed())
        {
            return true;
        }

        if (!HasFocus)
        {
            SetFocus();
        }

        if (gesture is MouseGesture.Click or MouseGesture.DoubleClick)
        {
            base.OnMouseEvent(mouse);
        }

        GestureRaised?.Invoke(gesture);
        return true;
    }
}
