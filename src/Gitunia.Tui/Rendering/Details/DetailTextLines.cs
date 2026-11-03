using Gitunia.Core.Visual;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>The rows of the Details tab as text lines: what a character-wise selection reads and copies.</summary>
/// <param name="content">The rows of the pane.</param>
public sealed class DetailTextLines(DetailContent content) : ITextLines
{
    public int Count => content.Rows.Count;

    public string TextOf(int line) => TextOf(content, line);

    public bool IsSkipped(int line) => false;

    /// <summary>The text of a row as drawn, without cutting it to a width.</summary>
    /// <param name="content">The rows of the pane.</param>
    /// <param name="row">The row.</param>
    public static string TextOf(DetailContent content, int row) =>
        row >= 0 && row < content.Rows.Count
            ? string.Concat(content.Rows[row].Segments.Select(segment => segment.Text))
            : string.Empty;
}
