using Gitunia.App;
using Gitunia.Core.Shell;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The window changed size: the shell state learns it (so it can refuse a confirm dialog that does not fit and close one
/// that no longer does), the overlays place themselves again and the diff pane follows in a later iteration.
/// </summary>
public sealed class ScreenResizeCoordinator
{
    private readonly Dispatcher _host;
    private readonly PaneChrome _chrome;
    private readonly ResizeTargets _targets;

    /// <param name="host">Takes the new size.</param>
    /// <param name="chrome">The window, whose viewport changes with the terminal.</param>
    /// <param name="terminal">Runs the diff pane's follow-up in a later iteration, so keys are handled in between.</param>
    /// <param name="targets">The views that place themselves again.</param>
    public ScreenResizeCoordinator(Dispatcher host, PaneChrome chrome, TerminalHost terminal, ResizeTargets targets)
    {
        _host = host;
        _chrome = chrome;
        _targets = targets;
        chrome.Window.ViewportChanged += (_, _) => terminal.DeferToNextIteration(targets.DiffPane.OnScreenResized);
        chrome.Window.ViewportChanged += (_, _) => ReportScreenSize();
        chrome.Window.ViewportChanged += (_, _) => targets.Overlays.OnScreenResized();
        chrome.Window.ViewportChanged += (_, _) => targets.Popups.Sync();
    }

    private void ReportScreenSize()
    {
        var size = _chrome.ScreenSize;
        _host.Dispatch(new ScreenResizedCommand(new ScreenExtent(size.Width, size.Height), _host.Clock.GetUtcNow()));
        _targets.Settings.OnScreenResized();
    }
}
