namespace Gitunia.Git.Status;

/// <summary>Record type prefixes and field layout of <c>git status --porcelain=v2</c>; see "Porcelain Format Version 2" in git-status(1).</summary>
public static class StatusRecordTypes
{
    public const char Ordinary = '1';
    public const char RenamedOrCopied = '2';
    public const char Unmerged = 'u';
    public const char Untracked = '?';
    public const char Ignored = '!';

    /// <summary>Header lines start with this.</summary>
    public const string HeaderPrefix = "# ";

    /// <summary>The submodule field is <c>N...</c> or <c>S&lt;c&gt;&lt;m&gt;&lt;u&gt;</c>.</summary>
    public const int SubmoduleFieldLength = 4;

    public const char SubmoduleMarker = 'S';
    public const int SubmoduleCommitChangedIndex = 1;
    public const int SubmoduleModifiedIndex = 2;
    public const int SubmoduleUntrackedIndex = 3;
    public const char SubmoduleCommitChanged = 'C';
    public const char SubmoduleModified = 'M';
    public const char SubmoduleUntracked = 'U';
}
