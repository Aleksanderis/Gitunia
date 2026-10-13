using System.Collections.Immutable;
using Gitunia.Core.Workspaces;

namespace Gitunia.Core.Picker;

/// <summary>
/// Finds repositories below scan roots by looking for a <c>.git</c> entry, without running git. Bounded in depth and count,
/// skips hidden and dependency directories and symbolic links, and never descends into a repository it found.
/// </summary>
public static class RepositoryScanner
{
    public const int DefaultMaximumDepth = 3;
    public const int DefaultMaximumResults = 2000;

    private static readonly string[] DefaultRootNames =
        ["Projects", "projects", "src", "code", "Code", "dev", "Dev", "repos", "Repos", "git", "source", "workspace", "work"];

    private static readonly HashSet<string> SkippedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", "target", "vendor", "packages", "dist", "build",
    };

    /// <summary>The scan roots used until configuration exists: the conventional source directories below the home directory that exist.</summary>
    public static ImmutableArray<string> DefaultRoots(string homeDirectory, Func<string, bool> directoryExists) =>
        [.. DefaultRootNames.Select(name => Path.Combine(homeDirectory, name)).Where(directoryExists)];

    /// <summary>Scans the roots and returns repository root paths sorted by name. Unreadable directories are skipped.</summary>
    public static ImmutableArray<string> Scan(
        IEnumerable<string> roots,
        int maximumDepth,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        var found = new List<string>();
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System };
        foreach (var root in roots)
        {
            Visit(root, 0, maximumDepth, maximumResults, options, found, cancellationToken);
        }

        found.Sort((left, right) => string.Compare(Path.GetFileName(left), Path.GetFileName(right), StringComparison.OrdinalIgnoreCase));
        return [.. found.Distinct(RepositoryPathComparer.Instance)];
    }

    private static void Visit(string directory, int depth, int maximumDepth, int maximumResults, EnumerationOptions options, List<string> found, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (found.Count >= maximumResults)
        {
            return;
        }

        var marker = Path.Combine(directory, ".git");
        if (Directory.Exists(marker) || File.Exists(marker))
        {
            found.Add(directory);
            return;
        }

        if (depth >= maximumDepth)
        {
            return;
        }

        try
        {
            foreach (var child in Directory.EnumerateDirectories(directory, "*", options))
            {
                var name = Path.GetFileName(child);
                if (name.StartsWith('.') || SkippedNames.Contains(name))
                {
                    continue;
                }

                Visit(child, depth + 1, maximumDepth, maximumResults, options, found, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A directory that vanishes or cannot be read is just not scanned.
        }
    }
}
