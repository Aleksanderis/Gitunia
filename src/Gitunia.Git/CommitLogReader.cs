using System.Runtime.CompilerServices;

namespace Gitunia.Git;

/// <summary>Streams the commit log of a repository as <see cref="CommitInfo"/> without buffering all of it.</summary>
public static class CommitLogReader
{
    /// <param name="revisions">Revision arguments such as <c>--all</c>, <c>HEAD</c> or a branch name; every ref except the stash if empty (stash commits are not history).</param>
    /// <param name="warnings">Receives the parser's warnings (skipped or truncated records) once the stream ends.</param>
    public static async IAsyncEnumerable<CommitInfo> ReadAsync(
        Repo repo,
        CommitOrder order,
        IReadOnlyList<string> revisions,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        ICollection<string>? warnings = null)
    {
        var arguments = CommitLogFormat.Arguments(order, revisions.Count == 0 ? ["--exclude=refs/stash", "--all"] : revisions);
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
}
