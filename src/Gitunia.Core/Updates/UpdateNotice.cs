namespace Gitunia.Core.Updates;

/// <summary>A newer release than the running build, as the status bar tells it.</summary>
/// <param name="Version">The newer version, as written on the release (<c>0.9.0</c>).</param>
/// <param name="Command">What to run to get it: <c>gitunia update</c>, or the package manager's command for this install.</param>
public sealed record UpdateNotice(string Version, string Command);
