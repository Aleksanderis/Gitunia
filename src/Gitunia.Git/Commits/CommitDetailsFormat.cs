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

    private static readonly string[] RenameArguments =
    [
        "diff-tree",
        "-r",
        GitOptions.NulTerminated,
        "--raw",
        "--no-abbrev",
        "--no-commit-id",
        "--find-renames",
    ];

    private static readonly string[] FileFixedArguments = [.. RenameArguments, "--find-copies"];

    /// <summary>
    /// Arguments for the metadata and the changed files of one commit in one process: the record in
    /// <see cref="Format"/>, a NUL, then the raw entries against the first parent (a root commit against the empty
    /// tree), renames and copies detected as in <see cref="FileArguments"/>.
    /// </summary>
    public static IReadOnlyList<string> CombinedArguments(ObjectId id) =>
    [
        "show",
        GitOptions.FirstParent,
        GitOptions.NulTerminated,
        $"--format={Format}",
        "--raw",
        "--no-abbrev",
        "--find-renames",
        "--find-copies",
        "--ignore-submodules=none",
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

    /// <summary>
    /// Like <see cref="FileArguments"/> but detecting renames only, as <c>git stash show</c> does (a staged copy of a
    /// modified file is an addition there).
    /// </summary>
    public static IReadOnlyList<string> RenameOnlyFileArguments(ObjectId id, ObjectId? firstParent)
    {
        return [.. RenameArguments, firstParent is { } parent ? parent.ToString() : RootOption, id.ToString()];
    }
}
