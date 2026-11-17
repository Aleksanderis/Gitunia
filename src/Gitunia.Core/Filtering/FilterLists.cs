using System.Collections.Immutable;
using Gitunia.Core.Editing;

namespace Gitunia.Core.Filtering;

/// <summary>Creates <see cref="FilterList{T}"/>s.</summary>
public static class FilterLists
{
    /// <summary>A list over <paramref name="items"/> filtered by <paramref name="field"/>, the best match highlighted.</summary>
    public static FilterList<T> Of<T>(
        ImmutableArray<FilterItem<T>> items,
        MatcherKind kind,
        string noMatch,
        TextField? field = null) =>
        new FilterList<T>(items, field ?? new TextField(), kind, noMatch, []).WithLeading([]);
}
