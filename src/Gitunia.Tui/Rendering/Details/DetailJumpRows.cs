using Gitunia.Core.Jump;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Files;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>
/// The rows of the detail pane as quick-jump rows: a file row (Changes tab) is its path after the change mark, any other row is
/// its text as drawn.
/// </summary>
/// <param name="content">The rows of the pane.</param>
/// <param name="width">Columns of the pane's text area.</param>
/// <param name="ellipsis">The ellipsis a cut row ends with.</param>
public sealed class DetailJumpRows(DetailContent content, int width, string ellipsis) : IJumpRows
{
    public int Count => content.Rows.Count;

    public int EllipsisWidth { get; } = TextWidth.Of(ellipsis);

    public string TextOf(int row) => TextOf(content, row, width, ellipsis);

    public int ColumnsOf(int row) => Math.Max(0, width - MarkColumns(content, row));

    /// <summary>The text a quick jump looks through in a row, and the one the label is drawn over.</summary>
    public static string TextOf(DetailContent content, int row)
    {
        var text = string.Concat(content.Rows[row].Segments.Select(segment => segment.Text));

        return text[MarkColumns(content, row)..];
    }

    /// <summary>
    /// The text of a row as drawn in <paramref name="width"/> columns: a file row in the name-first style and a Files
    /// tab row are laid out for that width, so the label lands on what is on screen.
    /// </summary>
    public static string TextOf(DetailContent content, int row, int width, string ellipsis)
    {
        if (content.Rows[row].File is { } file && content.PathStyle == FilePathStyle.NameFirst)
        {
            var columns = Math.Max(0, width - MarkColumns(content, row));

            return PathRowLayout.Text(content.PathStyle, file.Path, file.OldPath, columns, ellipsis);
        }

        if (content.Rows[row].Tree is { } tree)
        {
            return DetailPaneRenderer.RenderTreeRow(tree, false, width, ellipsis).Text.TrimEnd();
        }

        return TextOf(content, row);
    }

    /// <summary>The columns before <see cref="TextOf(DetailContent, int)"/> starts: the change mark and its space on a file row.</summary>
    public static int MarkColumns(DetailContent content, int row)
    {
        if (content.FileIndexAt(row) >= 0 && content.Rows[row].Segments.Count > 1)
        {
            return content.Rows[row].Segments[0].Text.Length + 1;
        }

        return 0;
    }
}
