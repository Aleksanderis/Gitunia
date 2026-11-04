using Gitunia.Core.Status;

namespace Gitunia.Core.Shell;

/// <summary>Show a message in the status bar and keep it in the history.</summary>
/// <param name="Text">The message.</param>
/// <param name="Severity">How it is drawn.</param>
/// <param name="At">When it was posted (the reducer reads no clock).</param>
/// <param name="Detail">Optional long text kept in the message history only (a restore command, a full hash).</param>
public sealed record StatusPostedCommand(string Text, StatusSeverity Severity, DateTimeOffset At, string? Detail = null)
    : ShellCommand;
