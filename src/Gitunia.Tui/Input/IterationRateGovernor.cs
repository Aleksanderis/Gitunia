namespace Gitunia.Tui.Input;

/// <summary>
/// Chooses how many main-loop iterations per second Terminal.Gui runs. Every iteration costs a few wakeups of three
/// threads even when nothing changed, so the full rate is kept only while the user is working and for
/// <paramref name="idleAfter"/> after the last key, mouse event or paste; an untouched screen drops to
/// <paramref name="idleRate"/> (idle CPU is about proportional to the rate, see docs/internal/performance.md). The first
/// input after an idle spell waits for one slower iteration, then the rate is back at <paramref name="activeRate"/>.
/// </summary>
/// <param name="activeRate">Iterations per second while the user is working (key latency).</param>
/// <param name="idleRate">Iterations per second on an untouched screen.</param>
/// <param name="idleAfter">How long without input until the idle rate applies.</param>
/// <param name="time">The clock; tests inject a fake one.</param>
public sealed class IterationRateGovernor(
    ushort activeRate,
    ushort idleRate,
    TimeSpan idleAfter,
    TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private long _lastInput = (time ?? TimeProvider.System).GetTimestamp();

    /// <summary>Records a key, mouse event or paste.</summary>
    public void NoteInput() => _lastInput = _time.GetTimestamp();

    /// <summary>The rate to use for the next iteration.</summary>
    public ushort CurrentRate() => _time.GetElapsedTime(_lastInput) < idleAfter ? activeRate : idleRate;
}
