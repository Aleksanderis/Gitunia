using System.Runtime.CompilerServices;

namespace Gitunia.Git.Tests.Fixtures;

/// <summary>
/// Makes <c>Path.GetTempPath()</c> return a path without symbolic links for the whole test process. On macOS the
/// temp directory is <c>/var/folders/...</c>, a symlink to <c>/private/var/folders/...</c>, and git reports the real
/// path (<c>rev-parse --show-toplevel</c>, <c>worktree list</c>), so every test that compares a path it built with
/// one git or the discovery returned would fail there (#959). Resolving it once up front keeps the tests free of
/// per-test path canonicalisation. Linux and Windows temp directories are normally already real, so this is a no-op.
/// </summary>
public static class RealTempDirectory
{
    private const string TempDirectoryVariable = "TMPDIR";

    [ModuleInitializer]
    internal static void Apply()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var real = Resolve(temp);

        if (real.Length > 0 && !string.Equals(real, temp, StringComparison.Ordinal))
        {
            Environment.SetEnvironmentVariable(TempDirectoryVariable, real + Path.DirectorySeparatorChar);
        }
    }

    // Resolves a symbolic link at each path component (enough for the temp directory; targets are not re-walked).
    private static string Resolve(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        var current = root;
        var relative = path[root.Length..];
        foreach (var part in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            var target = new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null)
            {
                current = target.FullName.TrimEnd(Path.DirectorySeparatorChar);
            }
        }

        return current;
    }
}
