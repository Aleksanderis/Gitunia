namespace Gitunia.Core.Shell;

/// <summary>Hide the message the status bar shows (the history keeps it).</summary>
/// <param name="At">When it was dismissed.</param>
public sealed record StatusDismissedCommand(DateTimeOffset At) : ShellCommand;
