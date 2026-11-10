using Gitunia.Platform.Updates;

namespace Gitunia.App.Updates;

/// <summary>The command that updates Gitunia for an install that a package manager owns. Pure.</summary>
public static class UpdateAdvice
{
    public const string ReleasesUrl = "https://github.com/Aleksanderis/Gitunia/releases";
    public const string InstallScriptCommand =
        "curl -fsSL https://raw.githubusercontent.com/Aleksanderis/Gitunia/main/scripts/install.sh | sh";

    /// <summary>The command to show, null when Gitunia replaces itself (<see cref="InstallRoute.Script"/>) or the route is unknown.</summary>
    public static string? CommandFor(InstallRoute route) => route switch
    {
        InstallRoute.Homebrew => "brew upgrade gitunia",
        InstallRoute.Winget => "winget upgrade Gitunia.Gitunia",
        InstallRoute.Pacman => $"yay -S gitunia-bin (AUR), or {InstallScriptCommand}",
        InstallRoute.Apt => InstallScriptCommand,
        InstallRoute.Dnf => InstallScriptCommand,
        _ => null,
    };
}
