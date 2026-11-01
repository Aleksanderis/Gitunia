using Gitunia.Core.Graph;
using Gitunia.Git;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.App.Loading.Graph;

/// <summary>
/// The read of the refs of the commits that carry one, started together with a log read: the layout needs the branch
/// names for the colours of its lanes. A failure of this read must not stop the graph from loading, so it ends in a
/// null result and the lanes are coloured by their first commit. Disposing cancels a read that is still running (the
/// log read failed or the consumer stopped early).
/// </summary>
public sealed class BranchTipRead : IDisposable
{
    private readonly CancellationTokenSource _cancellation;
    private readonly int _colorCount;

    /// <param name="commitLog">Where the refs come from.</param>
    /// <param name="location">The repository to read.</param>
    /// <param name="colorCount">Distinct colours of the palette, for the layout builder.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public BranchTipRead(
        ICommitLogReader commitLog,
        RepositoryLocation location,
        int colorCount,
        CancellationToken cancellationToken)
    {
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _colorCount = colorCount;
        Decorations = ReadAsync(commitLog, location, _cancellation.Token);
    }

    /// <summary>The refs by commit, or null when git could not list them.</summary>
    public Task<Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>?> Decorations { get; }

    /// <summary>A layout builder that knows the branch of every tip (none when the refs could not be read).</summary>
    public async Task<GraphLayoutBuilder> CreateBuilderAsync()
    {
        var decorations = await Decorations.ConfigureAwait(false);
        if (decorations is null)
        {
            return new GraphLayoutBuilder(null, _colorCount);
        }

        var tips = BranchColors.TipsOf(decorations);

        return new GraphLayoutBuilder(tips, _colorCount, BranchColors.ExtraNamesOf(decorations));
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
    }

    private static async Task<Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>?> ReadAsync(
        ICommitLogReader commitLog,
        RepositoryLocation location,
        CancellationToken cancellationToken)
    {
        try
        {
            return await commitLog.ReadTipDecorationsAsync(location, cancellationToken).ConfigureAwait(false);
        }
        catch (GitException)
        {
            return null;
        }
    }
}
