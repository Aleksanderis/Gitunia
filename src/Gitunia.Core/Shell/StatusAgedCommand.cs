namespace Gitunia.Core.Shell;

/// <summary>Time passed: the message may have lost its highlight or expired, so the bar is drawn again.</summary>
/// <param name="At">The time now.</param>
public sealed record StatusAgedCommand(DateTimeOffset At) : ShellCommand;
