using Gitunia.Git;
using Gitunia.Git.Commits.Models;

namespace Gitunia.App.Loading.Commits;

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

    private readonly int _capacity;
    private readonly int _fileBudget;
    private readonly Dictionary<ObjectId, LinkedListNode<CommitDetails>> _nodes = [];
    private readonly LinkedList<CommitDetails> _recentFirst = new();
    private int _fileCount;

    public CommitDetailsCache(int capacity = DefaultCapacity, int fileBudget = DefaultFileBudget)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(fileBudget, 0);
        _capacity = capacity;
        _fileBudget = fileBudget;
    }

    public int Count => _nodes.Count;

    /// <summary>The changed files held by all entries.</summary>
    public int FileCount => _fileCount;

    /// <summary>Looks a commit up and marks it as the most recently used.</summary>
    public bool TryGet(ObjectId commit, out CommitDetails details)
    {
        if (_nodes.TryGetValue(commit, out var node))
        {
            _recentFirst.Remove(node);
            _recentFirst.AddFirst(node);
            details = node.Value;
            return true;
        }

        details = null!;
        return false;
    }

    /// <summary>Whether a commit is cached, without changing its recency.</summary>
    public bool Contains(ObjectId commit) => _nodes.ContainsKey(commit);

    /// <summary>Adds (or refreshes) an entry as the most recently used and evicts the least recently used ones beyond the bounds. A commit alone over the file budget is not kept.</summary>
    public void Add(CommitDetails details)
    {
        Remove(details.Id);
        if (details.Files.Count > _fileBudget)
        {
            return;
        }

        _nodes[details.Id] = _recentFirst.AddFirst(details);
        _fileCount += details.Files.Count;
        while (_nodes.Count > _capacity || _fileCount > _fileBudget)
        {
            Remove(_recentFirst.Last!.Value.Id);
        }
    }

    public void Clear()
    {
        _nodes.Clear();
        _recentFirst.Clear();
        _fileCount = 0;
    }

    private void Remove(ObjectId commit)
    {
        if (_nodes.Remove(commit, out var node))
        {
            _recentFirst.Remove(node);
            _fileCount -= node.Value.Files.Count;
        }
    }
}
