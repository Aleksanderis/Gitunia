using System.Text;
using Gitunia.Core.Diff;
using Gitunia.Core.Graph;
using Gitunia.Core.Lists;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App;

/// <summary>Builds the clipboard text of a Visual-mode range for each pane; pure, so it runs without a terminal.</summary>
public static class RangeTextBuilder
{
    /// <summary>One commit hash per line for the commits the range covers (the reserved leading rows hold none).</summary>
    public static string Graph(CommitGraph graph, RowRange range)
    {
        var builder = new StringBuilder();
        for (var item = range.First; item <= range.Last; item++)
        {
            var commitIndex = GraphListSource.CommitIndexOf(item);
            if (commitIndex >= 0 && commitIndex < graph.Count)
            {
                builder.Append(graph[commitIndex].Id).Append('\n');
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The diff rows the range touches, one per line with their <c>+</c>, <c>-</c> or space prefix and no gutter. The range is in screen lines;
    /// with word wrap on, a row that wraps over several lines is copied once. Notes and warnings are left out.
    /// </summary>
    public static string Diff(DiffListSource source, RowRange range)
    {
        var rows = source.Document.Rows;
        var builder = new StringBuilder();
        var first = source.RowOf(range.First);
        var last = source.RowOf(range.Last);
        for (var row = Math.Max(first, 0); row <= last && row < rows.Count; row++)
        {
            if (rows[row].Kind is not (DiffRowKind.Note or DiffRowKind.Warning))
            {
                builder.Append(DiffTextWriter.Line(rows[row])).Append('\n');
            }
        }

        return builder.ToString();
    }

    /// <summary>The text of the detail rows the range covers, one per line, without trailing spaces.</summary>
    public static string Detail(DetailListSource source, RowRange range)
    {
        var builder = new StringBuilder();
        var rows = source.Content.Rows;
        for (var row = Math.Max(range.First, 0); row <= range.Last && row < rows.Count; row++)
        {
            var line = new StringBuilder();
            foreach (var segment in rows[row].Segments)
            {
                line.Append(segment.Text);
            }

            builder.Append(line.ToString().TrimEnd()).Append('\n');
        }

        return builder.ToString();
    }
}
