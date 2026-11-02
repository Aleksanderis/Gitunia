namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// A short window after a click that closed or used an overlay, in which a double-click is ignored: its first click acted
/// on the overlay, so the second must not act on the pane that is now under the pointer.
/// </summary>
public sealed class OverlayClickGuard
{
    private const long WindowMilliseconds = 600;

    private long _clickTicks = long.MinValue / 2;

    /// <summary>A click that used or closed an overlay just happened.</summary>
    public void Mark() => _clickTicks = Environment.TickCount64;

    /// <summary>True while a double-click must be ignored.</summary>
    public bool IsGuarding => Environment.TickCount64 - _clickTicks < WindowMilliseconds;
}
