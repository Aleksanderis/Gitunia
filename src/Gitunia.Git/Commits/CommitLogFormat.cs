using Gitunia.Git.Commits.Models;

namespace Gitunia.Git.Commits;

/// <summary>The exact <c>git log</c> arguments <see cref="CommitLogParser"/> understands.</summary>
public static class CommitLogFormat
{
    /// <summary>Number of fields in <see cref="Format"/>.</summary>
    public const int FieldCount = 7;

    private const string Separator = GitSeparators.UnitSeparatorPlaceholder;

    /// <summary>Hash, parents, author name, author email, author time (raw: unix seconds and UTC offset), decorations, subject.</summary>
    public const string Format = $"tformat:%H{Separator}%P{Separator}%an{Separator}%ae{Separator}%ad{Separator}%D{Separator}%s";

    private const string TopologicalOrder = "--topo-order";
    private const string DateOrder = "--date-order";

    private static readonly string[] FixedArguments =
    [
        "log",
        GitOptions.NulTerminated,
        $"--format={Format}",
        "--date=raw",
        "--decorate=full",
        GitOptions.NoColor,
        "--no-show-signature",
        "--encoding=UTF-8",
    ];

    /// <summary>Arguments for <c>git log</c>: parseable output, children before parents, full ref names in decorations.</summary>
    public static IReadOnlyList<string> Arguments(CommitOrder order, IReadOnlyCollection<string> revisions) =>
    [
        .. FixedArguments,
        order == CommitOrder.Topological ? TopologicalOrder : DateOrder,
        .. revisions,

        // Without '--' a revision that matches a file name is ambiguous to git.
        GitOptions.EndOfOptions,
    ];
}
