using System.Runtime.CompilerServices;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Commits;

/// <summary>Streams the commit log of a repository as <see cref="CommitInfo"/> without buffering all of it.</summary>
public sealed class CommitLogReader : ICommitLogReader
{
    private static readonly string[] DefaultRevisions = [$"--exclude={GitRefPrefixes.Stash}", "--all"];

    private static readonly string[] CountArguments = ["rev-list", "--count", .. DefaultRevisions, GitOptions.EndOfOptions];

    private static readonly string[] TipRevisions = ["--no-walk", .. DefaultRevisions];

    // Every commit that descends from HEAD, parents first, each line followed by its parents. HEAD itself is excluded,
    // and the result must not depend on which refs reach a commit, so the walk follows all parents (a first-parent
    // walk would drop a commit that only a merge reaches).
    private static readonly string[] DescendantsArguments =
    [
        "rev-list", "--parents", "--topo-order", "--reverse", "--ancestry-path", .. DefaultRevisions,
        $"{GitOptions.ExcludeRevisionPrefix}{GitOptions.HeadRevision}", GitOptions.EndOfOptions,
    ];

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

    public async Task<ObjectId?> ReadCurrentBranchTipAsync(RepositoryLocation location, CancellationToken cancellationToken)
    {
        Dictionary<ObjectId, IReadOnlyList<CommitDecoration>> decorations;
        try
        {
            decorations = await ReadTipDecorationsAsync(location, cancellationToken).ConfigureAwait(false);
        }
        catch (GitException)
        {
            // Only the lane of the current branch depends on this, so a failure must not stop the graph from loading.
            return null;
        }

        foreach (var (id, list) in decorations)
        {
            for (var index = 0; index < list.Count; index++)
            {
                if (list[index].IsCurrent && list[index].Kind == RefKind.LocalBranch)
                {
                    return id;
                }
            }
        }

        // A detached HEAD shows a RefKind.Head decoration instead.
        return null;
    }

    public async Task<HeadLayoutInfo> ReadHeadLayoutAsync(
        RepositoryLocation location,
        CancellationToken cancellationToken)
    {
        // Both runs start at once: the descendants of HEAD need no tip id, they are matched to it afterwards.
        var descendantsTask = ReadDescendantsOutputAsync(location, cancellationToken);
        var head = await ReadCurrentBranchTipAsync(location, cancellationToken).ConfigureAwait(false);
        var output = await descendantsTask.ConfigureAwait(false);
        if (head is null)
        {
            return HeadLayoutInfo.None;
        }

        return new HeadLayoutInfo(head, output is null ? null : FirstParentDescendantsParser.Parse(output, head.Value));
    }

    // Null when git failed (an unborn HEAD does): only the shape of the lane layout depends on this, so a failure must
    // not stop the graph from loading.
    private async Task<string?> ReadDescendantsOutputAsync(
        RepositoryLocation location,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _runner
                .RunAsync(location.CommandPath, DescendantsArguments, GitRunOptions.ReadOnlyRun, cancellationToken)
                .ConfigureAwait(false);
            return result.StandardOutputText;
        }
        catch (GitException)
        {
            return null;
        }
    }
}
