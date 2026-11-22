namespace Gitunia.Platform.Updates;

/// <summary>Reads the <see cref="InstallFacts"/> of the running process from the file system.</summary>
public sealed class InstallFactsReader(string? executablePath = null, Func<string?>? homeDirectory = null)
{
    private const string OsReleasePath = "/etc/os-release";

    private readonly string? _executablePath = executablePath ?? Environment.ProcessPath;
    private readonly Func<string?> _homeDirectory = homeDirectory ?? DefaultHomeDirectory;

    /// <summary>The operating system family this process runs on.</summary>
    public static HostSystem CurrentSystem()
    {
        if (OperatingSystem.IsWindows())
        {
            return HostSystem.Windows;
        }

        return OperatingSystem.IsMacOS() ? HostSystem.MacOs : HostSystem.Linux;
    }

    /// <summary>The facts of this process, or null when the path of the binary is unknown.</summary>
    public InstallFacts? Read()
    {
        var executable = _executablePath;
        if (string.IsNullOrEmpty(executable))
        {
            return null;
        }

        var system = CurrentSystem();
        var directory = Path.GetDirectoryName(executable) ?? string.Empty;
        var osRelease = system == HostSystem.Linux ? ReadOrNull(OsReleasePath) : null;

        return new InstallFacts(
            executable,
            ReadOrNull(Path.Combine(directory, InstallMarker.FileName)),
            system,
            osRelease,
            FindOtherScriptCopy(executable, system));
    }

    private static string? DefaultHomeDirectory() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // The script's default folder on Linux and macOS (Windows installs per user and has no second copy to warn about).
    private string? FindOtherScriptCopy(string executable, HostSystem system)
    {
        if (system == HostSystem.Windows)
        {
            return null;
        }

        var home = _homeDirectory();
        if (string.IsNullOrEmpty(home))
        {
            return null;
        }

        var folder = Path.Combine(home, ".local", "bin");
        var candidate = Path.Combine(folder, ReleaseTargetBinaryName);
        if (string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(executable), StringComparison.Ordinal))
        {
            return null;
        }

        var marker = ReadOrNull(Path.Combine(folder, InstallMarker.FileName));

        return marker is not null && marker.Trim() == InstallMarker.ScriptValue && File.Exists(candidate) ? candidate : null;
    }

    private const string ReleaseTargetBinaryName = "gitunia";

    private static string? ReadOrNull(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
