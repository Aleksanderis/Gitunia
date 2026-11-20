using Gitunia.Platform.Updates;

namespace Gitunia.App.Updates;

/// <summary>The command that updates Gitunia for an install that a package manager owns. Pure.</summary>
public static class UpdateAdvice
{
    public const string UpdateCommandText = "gitunia update";
    public const string ReleasesUrl = "https://github.com/Aleksanderis/Gitunia/releases";
    public const string InstallScriptCommand =
        "curl -fsSL https://raw.githubusercontent.com/Aleksanderis/Gitunia/main/scripts/install.sh | sh";

    /// <summary>The install script run for the release package: the script installs the archive unless told otherwise (#1473).</summary>
    public const string PackageInstallScriptCommand =
        "curl -fsSL https://raw.githubusercontent.com/Aleksanderis/Gitunia/main/scripts/install.sh | GITUNIA_METHOD=package sh";

    /// <summary>The command to show, null when Gitunia replaces itself (<see cref="InstallRoute.Script"/>) or the route is unknown.</summary>
    public static string? CommandFor(InstallRoute route) => route switch
    {
        InstallRoute.Homebrew => "brew upgrade gitunia",
        InstallRoute.Winget => "winget upgrade Gitunia.Gitunia",
        InstallRoute.Pacman => PackageInstallScriptCommand,
        InstallRoute.Apt => PackageInstallScriptCommand,
        InstallRoute.Dnf => PackageInstallScriptCommand,
        _ => null,
    };

    /// <summary>
    /// The command that removes a Linux package so the self-updating install can take its place, null for the other routes.
    /// </summary>
    public static string? RemovePackageCommandFor(InstallRoute route) => route switch
    {
        InstallRoute.Pacman => "sudo pacman -R gitunia",
        InstallRoute.Apt => "sudo apt remove gitunia",
        InstallRoute.Dnf => "sudo dnf remove gitunia",
        _ => null,
    };

    /// <summary>
    /// The command the status bar names: the package manager's own for Homebrew and winget, otherwise <c>gitunia update</c>,
    /// which replaces a script install and prints the exact commands for the other routes (theirs are too long for a bar).
    /// </summary>
    public static string HintCommandFor(InstallRoute route) => route switch
    {
        InstallRoute.Homebrew or InstallRoute.Winget => CommandFor(route)!,
        _ => UpdateCommandText,
    };
}
