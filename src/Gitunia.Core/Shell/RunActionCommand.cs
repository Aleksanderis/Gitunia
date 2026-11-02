namespace Gitunia.Core.Shell;

/// <summary>
/// A callback bridge (known gap, only the terminal retry uses it): runs a callback through the runner. A confirm
/// dialog carries it as its accepted command. Goes away when that flow gets a plain accepted command.
/// </summary>
/// <param name="Run">What to run, on the UI thread.</param>
public sealed record RunActionCommand(Action Run) : ShellCommand;
