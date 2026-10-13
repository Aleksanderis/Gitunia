using Gitunia.Core.Diff;
using Gitunia.Git;

namespace Gitunia.Tui.Loading;

/// <summary>A diff kept by <see cref="DiffCache"/>: the whole commit against its first parent, not cut short.</summary>
/// <param name="Commit">The commit.</param>
/// <param name="Document">The rows of its diff.</param>
public sealed record DiffCacheEntry(ObjectId Commit, DiffDocument Document);
