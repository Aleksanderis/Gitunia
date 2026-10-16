using Gitunia.Git;

namespace Gitunia.Tui.Loading.Diff;

/// <summary>The cache of commit diffs (see <see cref="KeyedDiffCache{TKey}"/>): the diff of a commit never changes, so entries never go stale.</summary>
public sealed class DiffCache(int capacity = KeyedDiffCache<ObjectId>.DefaultCapacity, int rowBudget = KeyedDiffCache<ObjectId>.DefaultRowBudget)
    : KeyedDiffCache<ObjectId>(capacity, rowBudget);
