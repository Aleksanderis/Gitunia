namespace Gitunia.Core.Jump;

/// <summary>
/// The rows a quick jump looks through: what each row shows as text and how many columns of the pane that text may use.
/// A view adapts its own rows to this (commit subjects, diff lines, file paths) so the labeler stays the same everywhere.
/// </summary>
public interface IJumpRows
{
    /// <summary>How many rows there are.</summary>
    int Count { get; }

    /// <summary>Columns the ellipsis takes that a row's text ends with when it is cut at the right edge.</summary>
    int EllipsisWidth { get; }

    /// <summary>The text of a row as it is shown (tabs already expanded), the part a jump matches against.</summary>
    /// <param name="row">The row.</param>
    string TextOf(int row);

    /// <summary>Columns of the pane the text of <paramref name="row"/> can use: what is left after the columns the row draws before it.</summary>
    /// <param name="row">The row.</param>
    int ColumnsOf(int row);
}
