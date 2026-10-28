namespace Gitunia.Platform.Terminal;

/// <summary>
/// The terminal's input and output modes (line editing, echo, signals on Ctrl-C, newline translation). Captured once
/// while the terminal is still in the state the user's shell left it, put back whenever the terminal is handed to
/// another program, so the program always gets a normal terminal whatever the UI did to it.
/// </summary>
public interface ITerminalModes
{
    /// <summary>
    /// The current OS has an implementation; false means <see cref="Capture"/> and <see cref="Restore"/> do nothing.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Remembers the current modes; false when there is nothing to remember (no terminal, unsupported OS).
    /// </summary>
    bool Capture();

    /// <summary>Puts the remembered modes back; does nothing when nothing was captured.</summary>
    void Restore();
}
