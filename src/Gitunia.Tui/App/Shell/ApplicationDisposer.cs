using Gitunia.Platform.Terminal;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// Disposes a Terminal.Gui application. The toolkit stops its input thread by waiting for it, and on Windows that
/// thread can sit in a console read that nothing wakes (#1369: a lost wake-up, seen as a test host that never
/// exited), so a <see cref="TerminalInputWakeLoop"/> wakes it until the wait ends.
/// </summary>
public sealed class ApplicationDisposer(ITerminalInputWaker? inputWaker = null, TimeSpan? wakeInterval = null)
{
    /// <summary>
    /// Names a file that gets one line per disposal that needed a wake-up (the CI loop job reads it).
    /// </summary>
    public const string WakeLogVariable = "GITUNIA_WAKE_LOG";

    private static readonly TimeSpan DefaultWakeInterval = TimeSpan.FromMilliseconds(250);

    private readonly ITerminalInputWaker _inputWaker = inputWaker ?? new TerminalInputWaker();
    private readonly TimeSpan _wakeInterval = wakeInterval ?? DefaultWakeInterval;

    /// <summary>Disposes the application; a dispose that ends within the wake interval wakes nothing.</summary>
    /// <param name="application">The application to dispose.</param>
    public void Dispose(IDisposable application)
    {
        int wakes;
        using (var wakeLoop = new TerminalInputWakeLoop(_inputWaker, _wakeInterval))
        {
            application.Dispose();
            wakes = wakeLoop.WakeCount;
        }

        if (wakes > 0)
        {
            WriteWakeLog(wakes);
        }
    }

    private static void WriteWakeLog(int wakes)
    {
        var path = Environment.GetEnvironmentVariable(WakeLogVariable);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.AppendAllText(path, $"{DateTime.UtcNow:O} wakes={wakes}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Diagnostics only.
        }
    }
}
