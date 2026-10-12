namespace Gitunia.Core.Lists;

/// <summary>
/// A range of list rows selected in Visual mode: it starts at the row where the selection began (<paramref name="Anchor"/>) and
/// ends at the cursor, in either direction. Pure data; the views only ask which rows it covers.
/// </summary>
/// <param name="Anchor">The row the selection started on.</param>
/// <param name="Cursor">The row the cursor is on now.</param>
public readonly record struct RowRange(int Anchor, int Cursor)
{
    /// <summary>The first covered row.</summary>
    public int First => Math.Min(Anchor, Cursor);

    /// <summary>The last covered row.</summary>
    public int Last => Math.Max(Anchor, Cursor);

    /// <summary>How many rows are covered (at least 1).</summary>
    public int Count => Last - First + 1;

    public bool Contains(int row) => row >= First && row <= Last;

    /// <summary>The same selection with the cursor on <paramref name="row"/>.</summary>
    public RowRange MoveCursorTo(int row) => this with { Cursor = row };
}
