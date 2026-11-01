using Gitunia.Core.Diff;

namespace Gitunia.App.Loading.Diff;

/// <summary>A diff kept by <see cref="KeyedDiffCache{TKey}"/>: a whole diff that was not cut short.</summary>
/// <param name="Key">What the diff belongs to (a commit, or a file of the working tree).</param>
/// <param name="Document">The rows of its diff.</param>
/// <typeparam name="TKey">The key type.</typeparam>
public sealed record KeyedDiffCacheEntry<TKey>(TKey Key, DiffDocument Document);
