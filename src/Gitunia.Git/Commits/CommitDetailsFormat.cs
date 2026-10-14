namespace Gitunia.Git.Commits;

/// <summary>The exact git arguments <see cref="CommitDetailsParser"/> and <see cref="CommitFileChangeParser"/> understand.</summary>
public static class CommitDetailsFormat
{
    /// <summary>Number of fields in <see cref="Format"/>.</summary>
    public const int FieldCount = 9;

    private const string Separator = GitSeparators.UnitSeparatorPlaceholder;

    /// <summary>Hash, parents, author name, email, ISO time, committer name, email, ISO time, raw message (last, may contain anything but NUL).</summary>
    public const string Format = $"tformat:%H{Separator}%P{Separator}%an{Separator}%ae{Separator}%aI{Separator}%cn{Separator}%ce{Separator}%cI{Separator}%B";

    private const string RootOption = "--root";

    private static readonly string[] FileFixedArguments = ["diff-tree", "-r", GitOptions.NulTerminated, "--raw", "--no-abbrev", "--no-commit-id", "--find-renames", "--find-copies"];

    /// <summary>Arguments for the metadata of one commit.</summary>
    public static IReadOnlyList<string> MetadataArguments(ObjectId id) =>
    [
        "log",
        "-1",
        GitOptions.NulTerminated,
        $"--format={Format}",
        GitOptions.NoColor,
        "--no-show-signature",
        "--encoding=UTF-8",
        id.ToString(),
        GitOptions.EndOfOptions,
    ];

    /// <summary>Arguments for the changed files of one commit against its first parent (a root commit against the empty tree).</summary>
    /// <param name="id">The commit.</param>
    /// <param name="firstParent">Its first parent, null for a root commit. Named explicitly because <c>diff-tree</c> shows merges against every parent otherwise.</param>
    public static IReadOnlyList<string> FileArguments(ObjectId id, ObjectId? firstParent)
    {
        return [.. FileFixedArguments, firstParent is { } parent ? parent.ToString() : RootOption, id.ToString()];
    }
}
