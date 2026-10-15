using System.Runtime.CompilerServices;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Diff;

/// <summary>Streams the files of a diff as <see cref="DiffFile"/> without buffering all of it.</summary>
public sealed class DiffReader : IDiffReader
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

    /// <summary>Index of the first parent in a <c>rev-list --parents</c> line, which starts with the commit itself.</summary>
    private const int FirstParentField = 1;

    private static readonly string[] FirstParentArguments = ["rev-list", "--parents", "-n", "1"];

    private static readonly char[] FieldSeparators = [' ', '\n'];

    private readonly GitRunner _runner;

    public DiffReader(GitRunner runner)
    {
        _runner = runner;
    }

    public async IAsyncEnumerable<DiffFile> ReadAsync(
        RepositoryLocation location,
        IReadOnlyList<string> command,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
            await foreach (var chunk in _runner
                .StreamAsync(location.CommandPath, arguments, GitRunOptions.ReadOnlyRun, cancellationToken)
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

    public IAsyncEnumerable<DiffFile> ReadCommitAsync(
        RepositoryLocation location,
        ObjectId commit,
        ObjectId? firstParent,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        DiffOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var extra = (options ?? DiffOptions.Default).ToArguments();
        IReadOnlyList<string> command = firstParent is { } parent
            ? ["diff", .. extra, parent.ToString(), commit.ToString()]
            : ["show", "--format=", .. extra, commit.ToString()];
        return ReadAsync(location, command, warnings, maximumLinesPerFile, cancellationToken);
    }

    public async Task<ObjectId?> ReadFirstParentAsync(RepositoryLocation location, ObjectId commit, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(location.CommandPath, [.. FirstParentArguments, commit.ToString()], GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
        var fields = result.StandardOutputText.Split(FieldSeparators, StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > FirstParentField && ObjectId.TryParse(fields[FirstParentField], out var parent) ? parent : null;
    }
}
