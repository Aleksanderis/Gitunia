using Gitunia.Git.Execution;

namespace Gitunia.Tui.App.Shell;

/// <summary>How the shell was asked to start.</summary>
/// <param name="DriverName">The Terminal.Gui driver to force (smoke tests); null picks the platform default.</param>
/// <param name="HomeDirectory">The home directory for config paths and picker scans (smoke tests); null uses the user profile.</param>
/// <param name="GitExecutable">The git executable for hand-offs to the terminal, located once at startup.</param>
public sealed record TuiLaunch(
    string? DriverName,
    string? HomeDirectory,
    string GitExecutable = GitExecutableLocator.BareName);
