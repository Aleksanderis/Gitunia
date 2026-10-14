namespace Gitunia.Tui.Rendering.Graph;

/// <summary>
/// Where the columns of a graph row sit. A row is the lane area, then the message column and then each shown column
/// (author, date, hash) after one space; a width of 0 means the column is dropped. The space after the last column is blank.
/// </summary>
/// <param name="LaneWidth">Columns of the lane area.</param>
/// <param name="MessageWidth">Columns of the message column (ref decorations and subject).</param>
/// <param name="AuthorWidth">Columns of the author column, 0 when dropped.</param>
/// <param name="DateWidth">Columns of the date column, 0 when dropped.</param>
/// <param name="HashWidth">Columns of the hash column, 0 when dropped.</param>
public sealed record GraphColumns(int LaneWidth, int MessageWidth, int AuthorWidth, int DateWidth, int HashWidth);
