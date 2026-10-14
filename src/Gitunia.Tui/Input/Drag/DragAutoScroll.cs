namespace Gitunia.Tui.Input.Drag;

/// <summary>
/// The rule for scrolling a list while a drag holds the pointer outside it: how many rows the cursor moves per timer tick. Pure.
/// The step grows with the distance beyond the pane edge (1 row just outside, 1 more for every 2 further rows, capped).
/// </summary>
public static class DragAutoScroll
{
    /// <summary>How often the timer ticks while the pointer rests outside the pane.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(80);

    /// <summary>The most rows one tick moves.</summary>
    public const int MaxStep = 6;

    /// <summary>Signed rows to move per tick: negative above the pane, positive below, 0 while the pointer is inside it.</summary>
    public static int Step(int pointerRow, int visibleRows)
    {
        if (visibleRows <= 0)
        {
            return 0;
        }

        if (pointerRow < 0)
        {
            return -Magnitude(-pointerRow);
        }

        return pointerRow >= visibleRows ? Magnitude(pointerRow - visibleRows + 1) : 0;
    }

    private static int Magnitude(int distance)
    {
        return Math.Min(MaxStep, 1 + ((distance - 1) / 2));
    }
}
