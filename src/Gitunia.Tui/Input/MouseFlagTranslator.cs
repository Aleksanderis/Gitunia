using Gitunia.Core.Mouse;
using Terminal.Gui.Input;

namespace Gitunia.Tui.Input;

/// <summary>Turns Terminal.Gui mouse flags into the renderer-agnostic <see cref="MouseGesture"/> the mouse map takes.</summary>
public static class MouseFlagTranslator
{
    /// <summary>
    /// True for any left click report, including the double and triple click the toolkit sends for quick repeated clicks
    /// (#310): a button must count every one of them, not only the first of a burst.
    /// </summary>
    public static bool IsLeftClick(MouseFlags flags) =>
        flags.HasFlag(MouseFlags.LeftButtonClicked) || flags.HasFlag(MouseFlags.LeftButtonDoubleClicked) || flags.HasFlag(MouseFlags.LeftButtonTripleClicked);

    /// <summary>Returns false for events the mouse map has no use for (moves, presses, releases, other buttons, a sideways wheel).</summary>
    /// <param name="flags">The toolkit's flags.</param>
    /// <param name="gesture">The gesture.</param>
    public static bool TryTranslate(MouseFlags flags, out MouseGesture gesture)
    {
        // Terminal.Gui reports a sideways wheel as the vertical wheel plus Ctrl (WheeledLeft, WheeledRight) and Shift+wheel is the same idea.
        var isSideways = flags.HasFlag(MouseFlags.Ctrl) || flags.HasFlag(MouseFlags.Shift);
        if (isSideways && (flags.HasFlag(MouseFlags.WheeledUp) || flags.HasFlag(MouseFlags.WheeledDown)))
        {
            gesture = default;
            return false;
        }

        if (flags.HasFlag(MouseFlags.WheeledUp))
        {
            gesture = MouseGesture.WheelUp;
        }
        else if (flags.HasFlag(MouseFlags.WheeledDown))
        {
            gesture = MouseGesture.WheelDown;
        }
        else if (flags.HasFlag(MouseFlags.LeftButtonDoubleClicked))
        {
            gesture = MouseGesture.DoubleClick;
        }
        else if (flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            gesture = MouseGesture.Click;
        }
        else
        {
            gesture = default;
            return false;
        }

        return true;
    }
}
