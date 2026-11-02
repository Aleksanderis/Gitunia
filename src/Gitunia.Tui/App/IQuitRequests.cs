namespace Gitunia.Tui.App;

/// <summary>Asks the application to quit.</summary>
public interface IQuitRequests
{
    /// <summary>
    /// A normal quit: the guard of an unsaved session applies (the first quit warns, the next one leaves, #369).
    /// </summary>
    void Request();

    /// <summary>
    /// A forced quit (Ctrl-c, #856): works in every mode and modal; running git work is cancelled once the quit goes
    /// ahead.
    /// </summary>
    void Force();
}
