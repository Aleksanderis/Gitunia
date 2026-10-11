using System.Runtime.CompilerServices;

namespace Gitunia.Git;

/// <summary>Streams the commit log of a repository as <see cref="CommitInfo"/> without buffering all of it.</summary>
public static class CommitLogReader
{
    /// <param name="revisions">Revision arguments such as <c>--all</c>, <c>HEAD</c> or a branch name; <c>--all</c> if empty.</param>
    public static async IAsyncEnumerable<CommitInfo> ReadAsync(
        Repo repo,
        CommitOrder order,
        IReadOnlyList<string> revisions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<string> arguments = CommitLogFormat.Arguments(order, revisions.Count == 0 ? ["--all"] : revisions);
        var parser = new CommitLogParser();
        var completed = new List<CommitInfo>();
        await foreach (byte[] chunk in repo.Runner
            .StreamAsync(repo.Location.CommandPath, arguments, new GitRunOptions(ReadOnly: true), cancellationToken)
            .ConfigureAwait(false))
        {
            parser.Feed(chunk, completed);
            foreach (CommitInfo commit in completed)
            {
                yield return commit;
            }

            completed.Clear();
        }

        parser.Complete();
    }
}
