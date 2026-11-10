namespace Gitunia.Platform.Updates;

/// <summary>
/// Decides the <see cref="InstallRoute"/>: the marker file wins, then the place of the binary (a package manager's
/// prefix), with the Linux distribution from <c>os-release</c> naming the manager. Pure, so every route is tested
/// without installing anything.
/// </summary>
public static class InstallRouteDetector
{
    private const char Slash = '/';
    private const char Backslash = '\\';
    private const string IdKey = "ID=";
    private const string IdLikeKey = "ID_LIKE=";

    private static readonly string[] HomebrewPrefixes = ["/opt/homebrew/", "/usr/local/cellar/", "/home/linuxbrew/.linuxbrew/"];
    private static readonly string[] WingetMarkers = ["/microsoft/winget/", "/windowsapps/"];
    private static readonly string[] SystemPackagePrefixes = ["/usr/bin/", "/bin/", "/usr/sbin/"];
    private static readonly string[] ArchIds = ["arch", "manjaro", "endeavouros", "cachyos", "omarchy"];
    private static readonly string[] DebianIds = ["debian", "ubuntu", "linuxmint", "pop", "raspbian"];
    private static readonly string[] RpmIds = ["fedora", "rhel", "centos", "rocky", "almalinux", "suse", "opensuse"];

    public static InstallRoute Detect(InstallFacts facts)
    {
        if (facts.MarkerText?.Trim() == InstallMarker.ScriptValue)
        {
            return InstallRoute.Script;
        }

        var path = facts.ExecutablePath.Replace(Backslash, Slash).ToLowerInvariant();

        return facts.System switch
        {
            HostSystem.Windows => DetectWindows(path),
            HostSystem.MacOs => DetectHomebrew(path),
            _ => DetectLinux(path, facts.OsReleaseText),
        };
    }

    private static InstallRoute DetectWindows(string path)
    {
        foreach (var marker in WingetMarkers)
        {
            if (path.Contains(marker, StringComparison.Ordinal))
            {
                return InstallRoute.Winget;
            }
        }

        return InstallRoute.Unknown;
    }

    private static InstallRoute DetectHomebrew(string path)
    {
        foreach (var prefix in HomebrewPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.Ordinal))
            {
                return InstallRoute.Homebrew;
            }
        }

        return InstallRoute.Unknown;
    }

    private static InstallRoute DetectLinux(string path, string? osRelease)
    {
        var homebrew = DetectHomebrew(path);
        if (homebrew != InstallRoute.Unknown)
        {
            return homebrew;
        }

        foreach (var prefix in SystemPackagePrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.Ordinal))
            {
                return ManagerOf(osRelease);
            }
        }

        return InstallRoute.Unknown;
    }

    private static InstallRoute ManagerOf(string? osRelease)
    {
        var ids = IdsOf(osRelease);
        if (ids.Overlaps(ArchIds))
        {
            return InstallRoute.Pacman;
        }

        if (ids.Overlaps(DebianIds))
        {
            return InstallRoute.Apt;
        }

        return ids.Overlaps(RpmIds) ? InstallRoute.Dnf : InstallRoute.Unknown;
    }

    // The ID and ID_LIKE values of os-release, lower case, quotes removed.
    private static HashSet<string> IdsOf(string? osRelease)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (osRelease is null)
        {
            return ids;
        }

        foreach (var line in osRelease.Split('\n'))
        {
            var trimmed = line.Trim();
            var value = ValueAfter(trimmed, IdKey) ?? ValueAfter(trimmed, IdLikeKey);
            if (value is null)
            {
                continue;
            }

            var words = value.Trim('"', '\'').ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            ids.UnionWith(words);
        }

        return ids;
    }

    private static string? ValueAfter(string line, string key)
    {
        return line.StartsWith(key, StringComparison.Ordinal) ? line[key.Length..] : null;
    }
}
