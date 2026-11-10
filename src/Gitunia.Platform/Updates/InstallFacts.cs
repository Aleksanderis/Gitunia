namespace Gitunia.Platform.Updates;

/// <summary>What <see cref="InstallRouteDetector"/> decides from, read once by <see cref="InstallFactsReader"/>.</summary>
/// <param name="ExecutablePath">The full path of the running binary.</param>
/// <param name="MarkerText">The text of the install-source marker file next to the binary, null when there is none.</param>
/// <param name="System">The operating system family.</param>
/// <param name="OsReleaseText">The text of <c>/etc/os-release</c> on Linux, null elsewhere or when unreadable.</param>
public sealed record InstallFacts(string ExecutablePath, string? MarkerText, HostSystem System, string? OsReleaseText);
