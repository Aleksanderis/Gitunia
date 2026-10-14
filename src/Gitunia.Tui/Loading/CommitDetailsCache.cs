using Gitunia.Git;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Tui.Loading;

/// <summary>
/// A least-recently-used cache of commit details, bounded twice: by entry count and by the number of changed files held
/// (a commit that touched thousands of files must not pin megabytes). Details of a commit never change, so entries never
/// go stale. Not thread-safe: UI thread only.
/// </summary>
public sealed class CommitDetailsCache
{
    /// <summary>Entries kept by default; the neighbours prefetched around the selection fit well inside it.</summary>
    public const int DefaultCapacity = 64;

    /// <summary>Changed files kept by default across all entries (a file change is roughly 100 to 200 bytes).</summary>
    public const int DefaultFileBudget = 20_000;

    private readonly int capacity;
    private readonly int fileBudget;
    private readonly Dictionary<ObjectId, LinkedListNode<CommitDetails>> nodes = [];
    private readonly LinkedList<CommitDetails> recentFirst = new();
    private int fileCount;

    public CommitDetailsCache(int capacity = DefaultCapacity, int fileBudget = DefaultFileBudget)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(fileBudget, 0);
        this.capacity = capacity;
        this.fileBudget = fileBudget;
    }

    public int Count => nodes.Count;

    /// <summary>The changed files held by all entries.</summary>
    public int FileCount => fileCount;

    /// <summary>Looks a commit up and marks it as the most recently used.</summary>
    public bool TryGet(ObjectId commit, out CommitDetails details)
    {
        if (nodes.TryGetValue(commit, out var node))
        {
            recentFirst.Remove(node);
            recentFirst.AddFirst(node);
            details = node.Value;
            return true;
        }

        details = null!;
        return false;
    }

    /// <summary>Whether a commit is cached, without changing its recency.</summary>
    public bool Contains(ObjectId commit) => nodes.ContainsKey(commit);

    /// <summary>Adds (or refreshes) an entry as the most recently used and evicts the least recently used ones beyond the bounds. A commit alone over the file budget is not kept.</summary>
    public void Add(CommitDetails details)
    {
        Remove(details.Id);
        if (details.Files.Count > fileBudget)
        {
            return;
        }

        nodes[details.Id] = recentFirst.AddFirst(details);
        fileCount += details.Files.Count;
        while (nodes.Count > capacity || fileCount > fileBudget)
        {
            Remove(recentFirst.Last!.Value.Id);
        }
    }

    public void Clear()
    {
        nodes.Clear();
        recentFirst.Clear();
        fileCount = 0;
    }

    private void Remove(ObjectId commit)
    {
        if (nodes.Remove(commit, out var node))
        {
            recentFirst.Remove(node);
            fileCount -= node.Value.Files.Count;
        }
    }
}
