using Gitunia.Platform.Terminal;
using Terminal.Gui.App;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// Disposes a Terminal.Gui application. The toolkit stops its input thread by waiting for it, and on Windows that
/// thread can sit in a console read that nothing wakes (#1369: a lost wake-up, seen as a test host that never
/// exited), so a <see cref="TerminalInputWakeLoop"/> wakes it until the wait ends.
/// </summary>
public sealed class ApplicationDisposer(ITerminalInputWaker? inputWaker = null)
{
    private static readonly TimeSpan WakeInterval = TimeSpan.FromMilliseconds(250);

    private readonly ITerminalInputWaker _inputWaker = inputWaker ?? new TerminalInputWaker();

    /// <summary>Disposes the application; a dispose that ends within the wake interval wakes nothing.</summary>
    /// <param name="application">The application to dispose.</param>
    public void Dispose(IApplication application)
    {
        using var wakeLoop = new TerminalInputWakeLoop(_inputWaker, WakeInterval);
        application.Dispose();
    }
}
