namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>Compares repository root paths: case-insensitive on Windows and macOS (default file systems), case-sensitive elsewhere, ignoring a trailing separator.</summary>
public sealed class RepositoryPathComparer : IEqualityComparer<string>
{
    private readonly StringComparer _comparer;

    private RepositoryPathComparer(StringComparer comparer) => _comparer = comparer;

    /// <summary>The comparer for the current operating system.</summary>
    public static RepositoryPathComparer Instance { get; } = Create(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS());

    /// <summary>A comparer with fixed case sensitivity, for tests of both behaviours.</summary>
    public static RepositoryPathComparer Create(bool ignoreCase) =>
        new(ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <inheritdoc />
    public bool Equals(string? left, string? right) => _comparer.Equals(Normalize(left), Normalize(right));

    /// <inheritdoc />
    public int GetHashCode(string obj) => _comparer.GetHashCode(Normalize(obj)!);

    private static string? Normalize(string? path)
    {
        if (path is null)
        {
            return null;
        }

        var unified = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        return Path.TrimEndingDirectorySeparator(unified);
    }
}
