using Gitunia.Git.Commits.Models;

namespace Gitunia.Git.Commits;

/// <summary>The one-letter change codes used by <c>diff-tree --raw</c> and <c>status --porcelain=v2</c>.</summary>
public static class FileChangeLetters
{
    /// <summary>Status only: no change on this side.</summary>
    public const char Unmodified = '.';

    public const char Added = 'A';
    public const char Modified = 'M';
    public const char Deleted = 'D';
    public const char Renamed = 'R';
    public const char Copied = 'C';
    public const char TypeChanged = 'T';
    public const char Unmerged = 'U';

    /// <summary>Maps a letter to its kind; <paramref name="allowUnmodified"/> accepts <c>.</c> as well (status output only).</summary>
    public static FileChangeKind? KindOf(char letter, bool allowUnmodified) => letter switch
    {
        Added => FileChangeKind.Added,
        Modified => FileChangeKind.Modified,
        Deleted => FileChangeKind.Deleted,
        Renamed => FileChangeKind.Renamed,
        Copied => FileChangeKind.Copied,
        TypeChanged => FileChangeKind.TypeChanged,
        Unmerged => FileChangeKind.UpdatedButUnmerged,
        Unmodified when allowUnmodified => FileChangeKind.Unmodified,
        _ => null,
    };
}
