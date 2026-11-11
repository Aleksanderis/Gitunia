namespace Gitunia.Git.Execution;

/// <summary>
/// Finds the git executable once, from <c>PATH</c> only. The current directory is never searched: on Windows a bare
/// name given to <c>CreateProcess</c> would find a <c>git.exe</c> in the folder Gitunia was started in, so the result
/// is an absolute path, and relative or empty <c>PATH</c> entries (which mean the current directory) are skipped.
/// </summary>
public sealed class GitExecutableLocator
{
    public const string BareName = "git";

    private const string DefaultWindowsExtensions = ".COM;.EXE;.BAT;.CMD";
    private const string WindowsLauncherDirectory = "cmd";
    private const string WindowsRealDirectory = "mingw64";
    private const string WindowsRealSubdirectory = "bin";
    private const string WindowsExecutable = "git.exe";

    private readonly string? _path;
    private readonly string? _pathExtensions;
    private readonly bool _isWindows;

    /// <param name="path">The <c>PATH</c> value.</param>
    /// <param name="pathExtensions">The <c>PATHEXT</c> value (Windows only).</param>
    /// <param name="isWindows">Whether to apply the Windows rules (extensions, the Git for Windows launcher).</param>
    public GitExecutableLocator(string? path, string? pathExtensions, bool isWindows)
    {
        _path = path;
        _pathExtensions = pathExtensions;
        _isWindows = isWindows;
    }

    public static GitExecutableLocator FromEnvironment() => new(
        Environment.GetEnvironmentVariable("PATH"),
        Environment.GetEnvironmentVariable("PATHEXT"),
        OperatingSystem.IsWindows());

    /// <summary>The absolute path of git, or <see cref="BareName"/> when <c>PATH</c> holds none.</summary>
    public string Locate()
    {
        if (string.IsNullOrEmpty(_path))
        {
            return BareName;
        }

        var extensions = Extensions();
        foreach (var entry in _path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var directory = entry.Trim('"');
            if (!Path.IsPathFullyQualified(directory))
            {
                continue;
            }

            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, BareName + extension);
                if (IsExecutable(candidate))
                {
                    return _isWindows ? PreferRealGit(candidate) : candidate;
                }
            }
        }

        return BareName;
    }

    private string[] Extensions()
    {
        if (!_isWindows)
        {
            return [string.Empty];
        }

        var configured = string.IsNullOrWhiteSpace(_pathExtensions) ? DefaultWindowsExtensions : _pathExtensions;
        return configured.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // Git for Windows puts a launcher at <Git>\cmd\git.exe that starts <Git>\mingw64\bin\git.exe: skip one process start per call.
    private static string PreferRealGit(string found)
    {
        var directory = Path.GetDirectoryName(found);
        if (directory is null
            || !string.Equals(Path.GetFileName(directory), WindowsLauncherDirectory, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(found), WindowsExecutable, StringComparison.OrdinalIgnoreCase))
        {
            return found;
        }

        var root = Path.GetDirectoryName(directory);
        if (root is null)
        {
            return found;
        }

        var real = Path.Combine(root, WindowsRealDirectory, WindowsRealSubdirectory, WindowsExecutable);
        return File.Exists(real) ? real : found;
    }

    private bool IsExecutable(string candidate)
    {
        if (!File.Exists(candidate))
        {
            return false;
        }

        const UnixFileMode anyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        return _isWindows || OperatingSystem.IsWindows() || (File.GetUnixFileMode(candidate) & anyExecute) != 0;
    }
}
