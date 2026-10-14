using System.Runtime.CompilerServices;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories;

namespace Gitunia.Git.Commits;

/// <summary>Streams the commit log of a repository as <see cref="CommitInfo"/> without buffering all of it.</summary>
public static class CommitLogReader
{
    private static readonly string[] DefaultRevisions = [$"--exclude={GitRefPrefixes.Stash}", "--all"];

    /// <param name="revisions">Revision arguments such as <c>--all</c>, <c>HEAD</c> or a branch name; every ref except the stash if empty (stash commits are not history).</param>
    /// <param name="warnings">Receives the parser's warnings (skipped or truncated records) once the stream ends.</param>
    public static async IAsyncEnumerable<CommitInfo> ReadAsync(
        Repository repository,
        CommitOrder order,
        IReadOnlyList<string> revisions,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        ICollection<string>? warnings = null)
    {
        var arguments = CommitLogFormat.Arguments(order, revisions.Count == 0 ? DefaultRevisions : revisions);
        var parser = new CommitLogParser();
        var completed = new List<CommitInfo>();
        await foreach (var chunk in repository.Runner
            .StreamAsync(repository.Location.CommandPath, arguments, GitRunOptions.ReadOnlyRun, cancellationToken)
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

    /// <summary>Number of commits <see cref="ReadAsync"/> returns for the default revisions (every ref except the stash), or -1 when no number came back.</summary>
    public static async Task<int> CountAsync(Repository repository, CancellationToken cancellationToken)
    {
        var result = await repository.RunAsync(["rev-list", "--count", .. DefaultRevisions, GitOptions.EndOfOptions], GitRunOptions.ReadOnlyRun, cancellationToken).ConfigureAwait(false);
        return int.TryParse(result.StandardOutputText.Trim(), out var count) ? count : -1;
    }

    /// <summary>The decorations of the commits that carry a ref (a few hundred at most), exactly as <see cref="ReadAsync"/> reports them; reads no history.</summary>
    public static async Task<Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>> ReadTipDecorationsAsync(Repository repository, CancellationToken cancellationToken)
    {
        var decorations = new Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>();
        await foreach (var commit in ReadAsync(repository, CommitOrder.Topological, ["--no-walk", .. DefaultRevisions], cancellationToken).ConfigureAwait(false))
        {
            if (commit.Decorations.Count > 0)
            {
                decorations[commit.Id] = commit.Decorations;
            }
        }

        return decorations;
    }
}
