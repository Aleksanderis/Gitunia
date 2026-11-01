namespace Gitunia.Core.DiffPane;

/// <summary>What the Changes list's cursor is on.</summary>
/// <param name="FolderPath">The folder under the cursor (tree style), null on any other row.</param>
/// <param name="FilePath">The file under the cursor, null when it is on none.</param>
public sealed record DiffCursor(string? FolderPath, string? FilePath)
{
    /// <summary>The cursor is on no file or folder.</summary>
    public static DiffCursor None { get; } = new(null, null);

    /// <summary>The cursor is on a file or folder.</summary>
    public bool IsOnItem => FolderPath is not null || FilePath is not null;
}
