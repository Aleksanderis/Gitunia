using Gitunia.Git;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;

namespace Gitunia.Tui.Loading.Stash;

/// <summary>
/// Reads commits through <paramref name="commits"/>, except the stashes in <paramref name="registry"/>: their details
/// are the stash commit's own message and people with the files of <see cref="IStashDetailsReader"/> (tracked changes,
/// then untracked files), and no parents, so a stash is never offered as a merge to switch the parent of.
/// </summary>
public sealed class StashAwareCommitDetailsReader(
    ICommitDetailsReader commits,
    IStashDetailsReader stashes,
    StashRegistry registry) : ICommitDetailsReader
{
    /// <summary>Why a stash cannot be shown any more.</summary>
    public const string GoneMessage = "The stash is no longer in the stash list";

    public async Task<CommitDetails?> ReadAsync(
        RepositoryLocation location,
        ObjectId id,
        ICollection<string>? warnings = null,
        CancellationToken cancellationToken = default)
    {
        if (!registry.TryGet(id, out var entry))
        {
            return await commits.ReadAsync(location, id, warnings, cancellationToken).ConfigureAwait(false);
        }

        var files = await stashes.ReadFilesAsync(location, entry, warnings, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(GoneMessage);
        var header = await commits.ReadAsync(location, id, warnings, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(GoneMessage);

        return header with
        {
            Parents = [],
            Files = [.. files.Files.Select(file => file.Change)],
            IsStash = true,
            UntrackedFileCount = files.Files.Count(file => file.Origin == StashFileOrigin.Untracked),
        };
    }

    public Task<IReadOnlyList<CommitFileChange>> ReadFilesAsync(
        RepositoryLocation location,
        ObjectId id,
        ObjectId? parent,
        ICollection<string>? warnings = null,
        CancellationToken cancellationToken = default) =>
        commits.ReadFilesAsync(location, id, parent, warnings, cancellationToken);
}
