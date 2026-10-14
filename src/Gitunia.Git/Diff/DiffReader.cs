using System.Runtime.CompilerServices;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;

namespace Gitunia.Git.Diff;

/// <summary>Streams the files of a diff as <see cref="DiffFile"/> without buffering all of it.</summary>
public static class DiffReader
{
    /// <summary>Flags that make the patch text predictable whatever the user's git config says.</summary>
    private static readonly string[] FixedOptions =
    [
        GitOptions.NoColor,
        "--no-ext-diff",
        "--no-textconv",
        "--src-prefix=a/",
        "--dst-prefix=b/",
        "--find-renames",
        "--find-copies",
    ];

    /// <param name="command">A git command that prints a patch with its arguments, for example <c>["diff", "--cached"]</c>,
    /// <c>["diff", "HEAD", "--", "path"]</c> or <c>["show", "--format=", hash]</c>. The fixed options are inserted after the command name.</param>
    /// <param name="warnings">Receives the parser's warnings once the stream ends.</param>
    /// <param name="maximumLinesPerFile">Hunk lines kept per file; the rest are dropped and the file is marked <see cref="DiffFile.IsTruncated"/>.</param>
    public static async IAsyncEnumerable<DiffFile> ReadAsync(
        Repository repository,
        IReadOnlyList<string> command,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfZero(command.Count);
        var arguments = new List<string>(command.Count + FixedOptions.Length) { command[0] };
        arguments.AddRange(FixedOptions);
        for (var index = 1; index < command.Count; index++)
        {
            arguments.Add(command[index]);
        }

        var parser = new DiffParser(maximumLinesPerFile);
        var completed = new List<DiffFile>();
        try
        {
            await foreach (var chunk in repository.Runner
                .StreamAsync(repository.Location.CommandPath, arguments, new GitRunOptions(ReadOnly: true), cancellationToken)
                .ConfigureAwait(false))
            {
                parser.Feed(chunk, completed);
                foreach (var file in completed)
                {
                    yield return file;
                }

                completed.Clear();
            }

            parser.Complete(completed);
            foreach (var file in completed)
            {
                yield return file;
            }
        }
        finally
        {
            // Also runs when the caller stops reading early, so warnings seen so far are not lost.
            if (warnings is not null)
            {
                foreach (var warning in parser.Warnings)
                {
                    warnings.Add(warning);
                }
            }
        }
    }

    /// <summary>
    /// Streams the diff of a commit against its first parent (so merge commits match the file list), or the whole root commit
    /// when <paramref name="firstParent"/> is null.
    /// </summary>
    public static IAsyncEnumerable<DiffFile> ReadCommitAsync(
        Repository repository,
        ObjectId commit,
        ObjectId? firstParent,
        CancellationToken cancellationToken,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue)
    {
        IReadOnlyList<string> command = firstParent is { } parent
            ? ["diff", parent.ToString(), commit.ToString()]
            : ["show", "--format=", commit.ToString()];
        return ReadAsync(repository, command, cancellationToken, warnings, maximumLinesPerFile);
    }
}
