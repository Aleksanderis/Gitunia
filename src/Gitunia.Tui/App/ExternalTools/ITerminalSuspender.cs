namespace Gitunia.Tui.App.ExternalTools;

/// <summary>
/// Gives the terminal away for a while: the UI stops, <c>work</c> runs on the real terminal, the UI starts again.
/// </summary>
public interface ITerminalSuspender
{
    /// <summary>
    /// Asks to suspend. <paramref name="work"/> runs while the terminal is released; <paramref name="resumed"/> runs
    /// once the UI is back.
    /// </summary>
    void Suspend(Action work, Action resumed);
}
