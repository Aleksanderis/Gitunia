using System.Collections.Concurrent;
using Gitunia.Git;
using Gitunia.Git.Stash;

namespace Gitunia.Tui.Loading.Stash;

/// <summary>
/// The stashes one tab has shown, found by their commit hash. The details and diff stores of a tab know commits by
/// id only; the readers in front of them look a stash up here, so a stash is read like a commit. One registry per tab.
/// </summary>
public sealed class StashRegistry
{
    private readonly ConcurrentDictionary<string, StashEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>Makes <paramref name="entry"/> known by its commit hash.</summary>
    public void Add(StashEntry entry) => _entries[entry.CommitHash] = entry;

    /// <summary>Finds the stash whose commit is <paramref name="commit"/>.</summary>
    public bool TryGet(ObjectId commit, out StashEntry entry) => _entries.TryGetValue(commit.ToString(), out entry!);
}
