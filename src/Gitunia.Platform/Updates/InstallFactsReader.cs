namespace Gitunia.Platform.Updates;

/// <summary>Reads the <see cref="InstallFacts"/> of the running process from the file system.</summary>
public sealed class InstallFactsReader(string? executablePath = null)
{
    private const string OsReleasePath = "/etc/os-release";

    private readonly string? _executablePath = executablePath ?? Environment.ProcessPath;

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

        return new InstallFacts(executable, ReadOrNull(Path.Combine(directory, InstallMarker.FileName)), system, osRelease);
    }

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
