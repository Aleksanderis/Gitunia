using Gitunia.Git.Status.Models;

namespace Gitunia.Tui.Loading.Diff;

/// <summary>The files under a folder of a working-tree list, read as one combined diff.</summary>
/// <param name="Path">The folder path from the repository root.</param>
/// <param name="Entries">The status entries of the files under the folder, never empty.</param>
public sealed record WorkingDiffFolder(string Path, IReadOnlyList<StatusEntry> Entries)
{
    /// <summary>Two folders are the same request when they hold the same files in the same state.</summary>
    public bool Equals(WorkingDiffFolder? other) =>
        other is not null && Path == other.Path && Entries.SequenceEqual(other.Entries);

    public override int GetHashCode() => HashCode.Combine(Path, Entries.Count);
}
