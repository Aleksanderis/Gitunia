namespace Gitunia.Platform.Updates;

/// <summary>How the running Gitunia binary got onto the machine, which decides how it can be updated.</summary>
public enum InstallRoute
{
    /// <summary>Not recognised (a development build, a hand-copied archive): nothing is replaced automatically.</summary>
    Unknown,

    /// <summary>An install script put the release archive's binary here: it is replaced in place.</summary>
    Script,

    Pacman,
    Apt,
    Dnf,
    Homebrew,
    Winget,
}
