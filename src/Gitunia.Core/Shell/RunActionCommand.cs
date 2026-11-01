namespace Gitunia.Core.Shell;

/// <summary>
/// A bridge for controllers that have not moved to commands yet: runs a callback through the runner. A confirm
/// dialog of such a controller carries it as its accepted command. Goes away with the last such controller.
/// </summary>
/// <param name="Run">What to run, on the UI thread.</param>
public sealed record RunActionCommand(Action Run) : ShellCommand;
