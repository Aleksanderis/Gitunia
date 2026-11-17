namespace Gitunia.Core.Filtering;

/// <summary>Builds the items of a <see cref="FilterList{T}"/>.</summary>
public static class FilterItems
{
    /// <summary>An item matched on one text.</summary>
    public static FilterItem<T> Of<T>(T value, string text, int bonus = 0, string? group = null) =>
        new(value, [text], bonus, group);

    /// <summary>An item matched on two texts (a title and an identifier).</summary>
    public static FilterItem<T> Of<T>(T value, string text, string alternate, string? group = null) =>
        new(value, [text, alternate], 0, group);
}
