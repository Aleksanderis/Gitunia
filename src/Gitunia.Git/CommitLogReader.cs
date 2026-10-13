using System.Runtime.CompilerServices;

namespace Gitunia.Git;

/// <summary>Streams the commit log of a repository as <see cref="CommitInfo"/> without buffering all of it.</summary>
public static class CommitLogReader
{
    private static readonly string[] DefaultRevisions = ["--exclude=refs/stash", "--all"];

    /// <param name="revisions">Revision arguments such as <c>--all</c>, <c>HEAD</c> or a branch name; every ref except the stash if empty (stash commits are not history).</param>
    /// <param name="warnings">Receives the parser's warnings (skipped or truncated records) once the stream ends.</param>
    public static async IAsyncEnumerable<CommitInfo> ReadAsync(
        Repo repo,
        CommitOrder order,
        IReadOnlyList<string> revisions,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        ICollection<string>? warnings = null)
    {
        var arguments = CommitLogFormat.Arguments(order, revisions.Count == 0 ? DefaultRevisions : revisions);
        var parser = new CommitLogParser();
        var completed = new List<CommitInfo>();
        await foreach (var chunk in repo.Runner
            .StreamAsync(repo.Location.CommandPath, arguments, new GitRunOptions(ReadOnly: true), cancellationToken)
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
    public static async Task<int> CountAsync(Repo repo, CancellationToken cancellationToken)
    {
        var result = await repo.RunAsync(["rev-list", "--count", .. DefaultRevisions, "--"], new GitRunOptions(ReadOnly: true), cancellationToken).ConfigureAwait(false);
        return int.TryParse(result.StandardOutputText.Trim(), out var count) ? count : -1;
    }

    /// <summary>The decorations of the commits that carry a ref (a few hundred at most), exactly as <see cref="ReadAsync"/> reports them; reads no history.</summary>
    public static async Task<Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>> ReadTipDecorationsAsync(Repo repo, CancellationToken cancellationToken)
    {
        var decorations = new Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>();
        await foreach (var commit in ReadAsync(repo, CommitOrder.Topological, ["--no-walk", .. DefaultRevisions], cancellationToken).ConfigureAwait(false))
        {
            if (commit.Decorations.Count > 0)
            {
                decorations[commit.Id] = commit.Decorations;
            }
        }

        return decorations;
    }
}
