using System.Collections.Immutable;

namespace Gitunia.Core.Filtering;

/// <summary>An item of a <see cref="FilterList{T}"/> with the texts the query is matched against.</summary>
/// <param name="Value">The item.</param>
/// <param name="Texts">What the query may match; the best score of them counts.</param>
/// <param name="Bonus">Added to the score, to rank an item above equal ones.</param>
/// <param name="Group">Items of a group stay together, the group holding the best match first; null for no group.</param>
public readonly record struct FilterItem<T>(T Value, ImmutableArray<string> Texts, int Bonus = 0, string? Group = null);
