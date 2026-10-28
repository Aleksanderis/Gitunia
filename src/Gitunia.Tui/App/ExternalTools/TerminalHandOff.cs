using Gitunia.Platform.Terminal;

namespace Gitunia.Tui.App.ExternalTools;

/// <summary>
/// Runs a program on the real terminal between two lives of the UI. The terminal modes the shell left (captured before
/// the UI started) are put back before the program starts, so Enter, Ctrl-C and echo work whatever the UI driver left
/// behind (#743), and again after it ends, so the next UI driver starts from the normal modes too.
/// </summary>
public sealed class TerminalHandOff(ITerminalModes modes)
{
    public void Run(Action work)
    {
        modes.Restore();
        try
        {
            work();
        }
        finally
        {
            modes.Restore();
        }
    }
}
