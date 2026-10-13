namespace Gitunia.Git;

/// <summary>The exact <c>git log</c> arguments <see cref="CommitLogParser"/> understands.</summary>
public static class CommitLogFormat
{
    /// <summary>Unit separator between the fields of one commit.</summary>
    public const byte FieldSeparator = 0x1f;

    /// <summary>Commits are terminated by NUL (<c>log -z</c>).</summary>
    public const byte RecordTerminator = 0;

    /// <summary>Hash, parents, author name, author email, author time (raw: unix seconds and UTC offset), decorations, subject.</summary>
    public const string Format = "tformat:%H%x1f%P%x1f%an%x1f%ae%x1f%ad%x1f%D%x1f%s";

    /// <summary>Arguments for <c>git log</c>: parseable output, children before parents, full ref names in decorations.</summary>
    public static IReadOnlyList<string> Arguments(CommitOrder order, IEnumerable<string> revisions)
    {
        var arguments = new List<string>
        {
            "log",
            "-z",
            "--format=" + Format,
            "--date=raw",
            "--decorate=full",
            "--no-color",
            "--no-show-signature",
            "--encoding=UTF-8",
        };
        arguments.Add(order == CommitOrder.Topological ? "--topo-order" : "--date-order");
        arguments.AddRange(revisions);

        // Without '--' a revision that matches a file name is ambiguous to git.
        arguments.Add("--");
        return arguments;
    }
}
