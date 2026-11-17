namespace Gitunia.Tui.Input;

/// <summary>
/// A surface that shows a <see cref="Gitunia.Core.Filtering.FilterList{T}"/> and takes its keys: while one is open the keys
/// are looked up in the <c>FilterList</c> view (<c>filter.next</c>, <c>filter.previous</c> and the wheel), so the move keys
/// are keymap rows and can be remapped.
/// </summary>
public interface IFilterFocus
{
    /// <summary>A filter list is open on this surface.</summary>
    bool HasFilterList { get; }
}
