namespace Gitunia.Core.Shell;

/// <summary>The terminal has this size now. A confirm dialog that no longer fits is closed without answering.</summary>
/// <param name="Screen">The new size.</param>
/// <param name="At">When it happened, for the warning.</param>
public sealed record ScreenResizedCommand(ScreenExtent Screen, DateTimeOffset At) : ShellCommand;
