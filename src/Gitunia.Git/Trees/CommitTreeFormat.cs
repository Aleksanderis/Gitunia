namespace Gitunia.Git.Trees;

/// <summary>The exact git arguments <see cref="CommitTreeParser"/> understands.</summary>
public static class CommitTreeFormat
{
    /// <summary>Mode of a symbolic link.</summary>
    public const string SymlinkMode = "120000";

    /// <summary>Mode of a submodule entry.</summary>
    public const string SubmoduleMode = "160000";

    /// <summary>Mode of an executable file.</summary>
    public const string ExecutableMode = "100755";

    /// <summary>The size column of an entry that has none (a submodule).</summary>
    public const string NoSize = "-";

    /// <summary>The character between the metadata and the path of a record.</summary>
    public const byte PathSeparator = (byte)'\t';

    private const string ListTree = "ls-tree";
    private const string Recursive = "-r";
    private const string Long = "--long";

    /// <summary>Arguments that list every file of a commit, recursively, one NUL-terminated record each.</summary>
    public static IReadOnlyList<string> Arguments(ObjectId commit) =>
        [ListTree, Recursive, GitOptions.NulTerminated, Long, commit.ToString(), GitOptions.EndOfOptions];
}
