using Gitunia.Git.Execution;

namespace Gitunia.Git.Watching;

/// <summary>Parses <c>git ls-files -z</c> output into the set of directories that hold files.</summary>
public static class WorkingTreeDirectoryParser
{
    private const char GitSeparator = '/';
    private const byte Nul = 0;

    /// <summary>Turns NUL-separated repository-relative file paths into the absolute directories above them (the root excluded).</summary>
    public static IReadOnlyList<string> Parse(string workingTreePath, ReadOnlySpan<byte> output)
    {
        var directories = new HashSet<string>(StringComparer.Ordinal);
        var lastDirectory = string.Empty;
        while (!output.IsEmpty)
        {
            var end = output.IndexOf(Nul);
            var entry = end < 0 ? output : output[..end];
            output = end < 0 ? [] : output[(end + 1)..];
            var separator = entry.LastIndexOf((byte)GitSeparator);
            if (separator <= 0)
            {
                continue;
            }

            var directory = GitPathText.Decode(entry[..separator]);
            if (directory == lastDirectory)
            {
                continue;
            }

            lastDirectory = directory;
            AddWithParents(directories, directory);
        }

        var result = new List<string>(directories.Count);
        foreach (var directory in directories)
        {
            result.Add(Path.Combine(workingTreePath, directory.Replace(GitSeparator, Path.DirectorySeparatorChar)));
        }

        return result;
    }

    private static void AddWithParents(HashSet<string> directories, string directory)
    {
        while (directory.Length > 0 && directories.Add(directory))
        {
            var separator = directory.LastIndexOf(GitSeparator);
            directory = separator < 0 ? string.Empty : directory[..separator];
        }
    }
}
