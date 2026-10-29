using System.Runtime.CompilerServices;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Commits;

/// <summary>Streams the commit log of a repository as <see cref="CommitInfo"/> without buffering all of it.</summary>
public sealed class CommitLogReader : ICommitLogReader
{
    private static readonly string[] DefaultRevisions = [$"--exclude={GitRefPrefixes.Stash}", "--all"];

    private static readonly string[] CountArguments = ["rev-list", "--count", .. DefaultRevisions, GitOptions.EndOfOptions];

    private static readonly string[] TipRevisions = ["--no-walk", .. DefaultRevisions];

    private readonly GitRunner _runner;

    public CommitLogReader(GitRunner runner)
    {
        _runner = runner;
    }

    public async IAsyncEnumerable<CommitInfo> ReadAsync(
        RepositoryLocation location,
        CommitOrder order,
        IReadOnlyList<string> revisions,
        ICollection<string>? warnings = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var arguments = CommitLogFormat.Arguments(order, revisions.Count == 0 ? DefaultRevisions : revisions);
        var parser = new CommitLogParser();
        var completed = new List<CommitInfo>();
        await foreach (var chunk in _runner
            .StreamAsync(location.CommandPath, arguments, GitRunOptions.ReadOnlyRun, cancellationToken)
            .ConfigureAwait(false))
        {
            parser.Feed(chunk, completed);
            foreach (var commit in completed)
            {
                yield return commit;
            }

            completed.Clear();
        }

        parser.Complete();
        if (warnings is not null)
        {
            foreach (var warning in parser.Warnings)
            {
                warnings.Add(warning);
            }
        }
    }

    public async Task<int> CountAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(location.CommandPath, CountArguments, GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
        return int.TryParse(result.StandardOutputText.Trim(), out var count) ? count : -1;
    }

    public async Task<Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>> ReadTipDecorationsAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        var decorations = new Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>();
        await foreach (var commit in ReadAsync(location, CommitOrder.Topological, TipRevisions, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (commit.Decorations.Count > 0)
            {
                decorations[commit.Id] = commit.Decorations;
            }
        }

        return decorations;
    }
}
