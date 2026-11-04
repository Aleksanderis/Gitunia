using Gitunia.App;
using Gitunia.Core.Visual;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Panes;

namespace Gitunia.Tui.Input;

/// <summary>
/// What <c>Ctrl-c</c> does. With no selection it is the last-resort quit (#856). While a Visual selection is active the
/// first press copies it (the usual reflex after selecting text) and keeps it; a second press within
/// <see cref="QuitWindow"/> quits.
/// </summary>
public sealed class CtrlCHandler
{
    /// <summary>How long after a copying <c>Ctrl-c</c> the next one quits.</summary>
    public static readonly TimeSpan QuitWindow = TimeSpan.FromSeconds(2);

    private readonly Dispatcher _host;
    private readonly VisualRangeInput _visual;
    private readonly IQuitRequests _quit;
    private readonly TimeProvider _time;
    private DateTimeOffset? _copiedAt;

    /// <param name="host">Takes the copy request.</param>
    /// <param name="visual">Tells whether a selection is active.</param>
    /// <param name="quit">Quits.</param>
    /// <param name="time">The clock, for the window of the second press.</param>
    public CtrlCHandler(Dispatcher host, VisualRangeInput visual, IQuitRequests quit, TimeProvider? time = null)
    {
        _host = host;
        _visual = visual;
        _quit = quit;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Handles a <c>Ctrl-c</c> press.</summary>
    public void Press()
    {
        var now = _time.GetUtcNow();
        if (!_visual.IsActive || (_copiedAt is { } copied && now - copied <= QuitWindow))
        {
            _copiedAt = null;
            _quit.Force();

            return;
        }

        _copiedAt = now;
        _host.Dispatch(new VisualCopyRequested(VisualCopyMode.CtrlC));
    }
}
