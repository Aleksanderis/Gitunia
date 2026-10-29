using Gitunia.Git;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Trees;
using Gitunia.Git.Trees.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Serves the trees of commits from memory. A commit that was not added fails like git does for an unknown
/// commit.</summary>
public sealed class FakeCommitTreeReader : ICommitTreeReader
{
    private readonly Dictionary<ObjectId, IReadOnlyList<TreeEntry>> _trees = [];

    /// <summary>The number of reads so far.</summary>
    public int ReadCount { get; private set; }

    /// <summary>Gives <paramref name="commit"/> a tree of regular files with these paths (each 10 bytes).</summary>
    public void Add(ObjectId commit, params string[] paths) =>
        _trees[commit] = [.. paths.Select(path => new TreeEntry(path, TreeEntryKind.File, commit, 10))];

    public Task<IReadOnlyList<TreeEntry>> ReadAsync(
        RepositoryLocation location,
        ObjectId commit,
        ICollection<string>? warnings = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCount++;

        if (_trees.TryGetValue(commit, out var entries))
        {
            return Task.FromResult(entries);
        }

        throw new GitException($"unknown commit {commit}");
    }
}
