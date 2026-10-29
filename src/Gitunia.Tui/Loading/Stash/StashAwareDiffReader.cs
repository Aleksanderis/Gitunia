using System.Runtime.CompilerServices;
using Gitunia.Git;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Tui.Loading.Stash;

/// <summary>
/// Reads diffs through <paramref name="diffs"/>, except for the stashes in <paramref name="registry"/>: the diff of a
/// stash is its tracked changes followed by its untracked files as additions, in the order of the file list.
/// </summary>
public sealed class StashAwareDiffReader(IDiffReader diffs, IStashDetailsReader stashes, StashRegistry registry)
    : IDiffReader
{
    private static readonly StashFileOrigin[] Origins = [StashFileOrigin.Tracked, StashFileOrigin.Untracked];

    public IAsyncEnumerable<DiffFile> ReadAsync(
        RepositoryLocation location,
        IReadOnlyList<string> command,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        CancellationToken cancellationToken = default) =>
        diffs.ReadAsync(location, command, warnings, maximumLinesPerFile, cancellationToken);

    public IAsyncEnumerable<DiffFile> ReadCommitAsync(
        RepositoryLocation location,
        ObjectId commit,
        ObjectId? firstParent,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        DiffOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (registry.TryGet(commit, out var entry))
        {
            return ReadStashAsync(location, entry, warnings, maximumLinesPerFile, options, cancellationToken);
        }

        return diffs.ReadCommitAsync(
            location,
            commit,
            firstParent,
            warnings,
            maximumLinesPerFile,
            options,
            cancellationToken);
    }

    public IAsyncEnumerable<DiffFile> ReadWorkingFileAsync(
        RepositoryLocation location,
        WorkingDiffSide side,
        StatusEntry entry,
        ICollection<string>? warnings = null,
        int maximumLinesPerFile = int.MaxValue,
        DiffOptions? options = null,
        CancellationToken cancellationToken = default) =>
        diffs.ReadWorkingFileAsync(location, side, entry, warnings, maximumLinesPerFile, options, cancellationToken);

    public Task<ObjectId?> ReadFirstParentAsync(
        RepositoryLocation location,
        ObjectId commit,
        CancellationToken cancellationToken) =>
        diffs.ReadFirstParentAsync(location, commit, cancellationToken);

    private async IAsyncEnumerable<DiffFile> ReadStashAsync(
        RepositoryLocation location,
        StashEntry entry,
        ICollection<string>? warnings,
        int maximumLinesPerFile,
        DiffOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var origin in Origins)
        {
            await foreach (var file in stashes
                .ReadDiffAsync(location, entry, origin, warnings, maximumLinesPerFile, options, cancellationToken)
                .ConfigureAwait(false))
            {
                yield return file;
            }
        }
    }
}
