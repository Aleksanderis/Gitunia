namespace Gitunia.Git.Commits;

/// <summary>The exact git arguments <see cref="CommitDetailsParser"/> and <see cref="CommitFileChangeParser"/> understand.</summary>
public static class CommitDetailsFormat
{
    /// <summary>Unit separator between the fields of the metadata record.</summary>
    public const byte FieldSeparator = 0x1f;

    /// <summary>Hash, parents, author name, email, ISO time, committer name, email, ISO time, raw message (last, may contain anything but NUL).</summary>
    public const string Format = "tformat:%H%x1f%P%x1f%an%x1f%ae%x1f%aI%x1f%cn%x1f%ce%x1f%cI%x1f%B";

    /// <summary>Arguments for the metadata of one commit.</summary>
    public static IReadOnlyList<string> MetadataArguments(ObjectId id) =>
    [
        "log",
        "-1",
        "-z",
        "--format=" + Format,
        "--no-color",
        "--no-show-signature",
        "--encoding=UTF-8",
        id.ToString(),
        "--",
    ];

    /// <summary>Arguments for the changed files of one commit against its first parent (a root commit against the empty tree).</summary>
    /// <param name="id">The commit.</param>
    /// <param name="firstParent">Its first parent, null for a root commit. Named explicitly because <c>diff-tree</c> shows merges against every parent otherwise.</param>
    public static IReadOnlyList<string> FileArguments(ObjectId id, ObjectId? firstParent)
    {
        var arguments = new List<string> { "diff-tree", "-r", "-z", "--raw", "--no-abbrev", "--no-commit-id", "--find-renames", "--find-copies" };
        if (firstParent is { } parent)
        {
            arguments.Add(parent.ToString());
        }
        else
        {
            arguments.Add("--root");
        }

        arguments.Add(id.ToString());
        return arguments;
    }
}
