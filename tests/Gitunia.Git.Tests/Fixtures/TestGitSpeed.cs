namespace Gitunia.Git.Tests.Fixtures;

/// <summary>
/// Settings that make the git processes of tests cheaper without changing what they do (#624): the real git
/// executable instead of the Windows shim that starts it, and no background work (auto maintenance, fsync) that a
/// throwaway repository never needs. Shared by <c>TestRepository</c>, <c>IsolatedGit</c> and the graph fixture of the
/// Core tests.
/// </summary>
public static class TestGitSpeed
{
    private const string WindowsShimDirectory = "cmd";
    private const string WindowsRealDirectory = "mingw64";
    private const string WindowsExecutable = "git.exe";

    private static readonly Lazy<string> ExecutablePath = new(ResolveExecutable);

    /// <summary>
    /// Default timeout of the git runners of tests (production keeps its own 30 s). A small CI machine runs the test
    /// assemblies with thousands of git processes at once, and a <c>git init</c> then waits longer than 30 s for a CPU
    /// slice (#634). A genuinely hung git still fails the test, only later; tests of the timeout itself pass their own.
    /// </summary>
    public static TimeSpan Timeout { get; } = TimeSpan.FromMinutes(2);

    /// <summary>Config entries added to every test git process, as <c>GIT_CONFIG_KEY_n</c> pairs.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Settings { get; } =
    [
        new("gc.auto", "0"),
        new("maintenance.auto", "false"),
        new("core.fsync", "none"),
    ];

    /// <summary>
    /// <c>git</c>, or on Windows the <c>mingw64\bin\git.exe</c> next to the <c>cmd\git.exe</c> shim found on the PATH
    /// (the shim only starts the real one, which doubles the process creation cost under antivirus scanning). Other
    /// layouts (Scoop, portable installs) fall back to <c>git</c>, which is correct, only slower.
    /// </summary>
    public static string Executable => ExecutablePath.Value;

    private static string ResolveExecutable()
    {
        const string fallback = "git";
        if (!OperatingSystem.IsWindows())
        {
            return fallback;
        }

        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var directory in directories)
        {
            var shim = Path.Combine(directory, WindowsExecutable);
            var isShimDirectory = string.Equals(
                Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar)),
                WindowsShimDirectory,
                StringComparison.OrdinalIgnoreCase);
            if (!isShimDirectory || !File.Exists(shim))
            {
                continue;
            }

            var installation = Path.GetDirectoryName(directory.TrimEnd(Path.DirectorySeparatorChar));
            var real = installation is null
                ? null
                : Path.Combine(installation, WindowsRealDirectory, "bin", WindowsExecutable);

            return real is not null && File.Exists(real) ? real : fallback;
        }

        return fallback;
    }
}
