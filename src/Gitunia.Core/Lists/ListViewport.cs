using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Lists;

/// <summary>
/// The one place that decides which rows of a list show and where the selection moves: a pure value of
/// <c>(Selected, Top, Count, Rows)</c>. Moves stop at the ends (<see cref="ListMotion"/>); <see cref="Follow"/> keeps the
/// selection visible by a <see cref="ScrollPolicy"/>. A list surface uses this instead of its own clamp or scroll arithmetic.
/// </summary>
/// <param name="Selected">The selected item, clamped into the list.</param>
/// <param name="Top">The first shown item, always a valid window start.</param>
/// <param name="Count">Number of items.</param>
/// <param name="Rows">Rows the view can show, at least 1 and at most the item count (1 for an empty list).</param>
public readonly record struct ListViewport(int Selected, int Top, int Count, int Rows)
{
    /// <summary>The selection and top clamped into a list of <paramref name="count"/> items shown in <paramref name="rows"/> rows.</summary>
    public static ListViewport Of(int selected, int top, int count, int rows)
    {
        var total = Math.Max(0, count);
        var height = Math.Max(1, Math.Min(rows, Math.Max(1, total)));

        return new ListViewport(ClampIndex(selected, total), Math.Clamp(top, 0, Math.Max(0, total - height)), total, height);
    }

    /// <summary>A viewport starting at the first item.</summary>
    public static ListViewport Of(int selected, int count, int rows) => Of(selected, 0, count, rows);

    /// <summary>The item index clamped into a list of <paramref name="count"/> items (0 when it is empty).</summary>
    public static int ClampIndex(int index, int count) => count <= 0 ? 0 : Math.Clamp(index, 0, count - 1);

    /// <summary>The first shown item for <paramref name="selected"/> under <paramref name="policy"/> (see <see cref="Follow"/>).</summary>
    public static int TopFor(int selected, int top, int count, int rows, ScrollPolicy policy) =>
        Of(selected, top, count, rows).Follow(policy).Top;

    /// <summary>The rows shown, the selection and the first row: what a renderer reads.</summary>
    public ListWindow Window => new(Rows, Selected, Top);

    /// <summary>Whether <paramref name="item"/> is inside the window.</summary>
    public bool IsVisible(int item) => item >= Top && item < Top + Rows;

    /// <summary>The same list with <see cref="Top"/> moved so the selection shows.</summary>
    public ListViewport Follow(ScrollPolicy policy)
    {
        var last = Math.Max(0, Count - Rows);
        var top = policy == ScrollPolicy.Centre ? Selected - (Rows / 2) : EdgeTop();

        return this with { Top = Math.Clamp(top, 0, last) };
    }

    /// <summary>The viewport after a list motion (stops at the ends), or null when <paramref name="action"/> is not one.</summary>
    public ListViewport? Move(ActionId action, int? count, ScrollPolicy policy)
    {
        if (!ListMotion.TryMove(action, count, Selected, Count, Rows, out var target))
        {
            return null;
        }

        return (this with { Selected = target }).Follow(policy);
    }

    /// <summary>The selection moved by <paramref name="delta"/> rows, stopping at the ends.</summary>
    public ListViewport MoveBy(int delta, ScrollPolicy policy) =>
        (this with { Selected = ClampIndex(Selected + delta, Count) }).Follow(policy);

    /// <summary>
    /// The first shown row that keeps <paramref name="cursor"/> visible with the least movement from <paramref name="top"/>,
    /// for a text or list whose length the caller does not bound (an editor); never negative. A view without a rows
    /// count (<paramref name="rows"/> of 0 or less) starts at <paramref name="top"/>.
    /// </summary>
    public static int EdgeTopFor(int cursor, int top, int rows)
    {
        if (rows <= 0 || cursor < 0)
        {
            return Math.Max(0, top);
        }

        if (cursor < top)
        {
            return cursor;
        }

        return cursor >= top + rows ? cursor - rows + 1 : Math.Max(0, top);
    }

    private int EdgeTop() => EdgeTopFor(Selected, Top, Rows);
}
