using Gitunia.Core.Status;

namespace Gitunia.Core.Shell;

/// <summary>Show a message in the status bar and keep it in the history.</summary>
/// <param name="Text">The message.</param>
/// <param name="Severity">How it is drawn.</param>
/// <param name="At">When it was posted (the reducer reads no clock).</param>
public sealed record StatusPostedCommand(string Text, StatusSeverity Severity, DateTimeOffset At) : ShellCommand;
