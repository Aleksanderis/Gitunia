namespace Gitunia.Core.Shell;

/// <summary>A command of the quit guard: the program is asked to stop, or the answer to saving the session.</summary>
public abstract record QuitCommand : AppCommand;

/// <summary>The program is asked to stop (<c>q</c>, Esc in the picker without tabs, Ctrl-c).</summary>
/// <param name="HasUnsentMessage">Whether any tab holds an unsent commit message; false when the session is not saved
/// at all (nothing to lose).</param>
/// <param name="Force">Running git work is cancelled once the quit goes ahead (Ctrl-c, #856).</param>
public sealed record QuitRequested(bool HasUnsentMessage, bool Force) : QuitCommand;

/// <summary>Result: the session was saved now.</summary>
/// <param name="Succeeded">False when the save failed.</param>
/// <param name="Force">As in <see cref="QuitRequested"/>.</param>
public sealed record SessionFlushed(bool Succeeded, bool Force) : QuitCommand;

/// <summary>A commit draft changed: a new unsent message deserves its own warning.</summary>
public sealed record QuitWarningReset : QuitCommand;
