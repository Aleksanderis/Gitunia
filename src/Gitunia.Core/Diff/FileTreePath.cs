namespace Gitunia.Core.Diff;

/// <summary>Path tests for the folder view of the Changes tab; git paths always use <c>/</c>.</summary>
public static class FileTreePath
{
    private const char Separator = '/';

    /// <summary>Whether <paramref name="path"/> lies in <paramref name="folder"/> (at any depth).</summary>
    /// <param name="folder">The folder path from the repository root, without a trailing separator.</param>
    /// <param name="path">The path of a file.</param>
    public static bool IsUnder(string folder, string path) =>
        path.Length > folder.Length
        && path[folder.Length] == Separator
        && path.StartsWith(folder, StringComparison.Ordinal);
}
