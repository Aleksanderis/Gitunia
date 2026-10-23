using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Watching;

/// <summary>
/// A cheap change detector for polling: hashes the name, size and time of the files that carry repository state
/// (HEAD, index, refs, worktrees, operation files). Working tree edits are not covered; those need a status read.
/// </summary>
public static class RepositoryFingerprint
{
    /// <summary>A hash of name, size and time of the files that carry repository state.</summary>
    public static int Compute(RepositoryLocation location)
    {
        var hash = new HashCode();
        AddRootFiles(ref hash, location.GitDirectory);
        if (location.IsLinkedWorktree)
        {
            AddRootFiles(ref hash, location.CommonDirectory);
        }

        AddTree(ref hash, Path.Combine(location.CommonDirectory, "refs"));
        AddTree(ref hash, Path.Combine(location.CommonDirectory, "worktrees"));
        return hash.ToHashCode();
    }

    private static void AddRootFiles(ref HashCode hash, string directory)
    {
        try
        {
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                AddEntry(ref hash, entry);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            hash.Add(directory);
        }
    }

    private static void AddTree(ref HashCode hash, string directory)
    {
        try
        {
            var tree = new DirectoryInfo(directory);
            foreach (var entry in tree.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            {
                AddEntry(ref hash, entry);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            hash.Add(directory);
        }
    }

    private static void AddEntry(ref HashCode hash, FileSystemInfo entry)
    {
        if (entry.Name.EndsWith(".lock", StringComparison.Ordinal))
        {
            return;
        }

        hash.Add(entry.FullName);
        hash.Add(entry.LastWriteTimeUtc.Ticks);
        hash.Add(entry is FileInfo file ? file.Length : -1);
    }
}
