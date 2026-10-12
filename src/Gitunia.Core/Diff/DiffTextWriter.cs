using System.Text;

namespace Gitunia.Core.Diff;

/// <summary>Turns diff rows back into plain diff text for the clipboard.</summary>
public static class DiffTextWriter
{
    /// <summary>One row as it reads in a diff: added, removed and context rows get their <c>+</c>, <c>-</c> or space prefix back.</summary>
    public static string Line(DiffRow row) => row.Kind switch
    {
        DiffRowKind.Added => "+" + row.Text,
        DiffRowKind.Removed => "-" + row.Text,
        DiffRowKind.Context => " " + row.Text,
        _ => row.Text,
    };

    /// <summary>All rows of the file <paramref name="fileIndex"/>, one per line; empty when the document has no such file.</summary>
    public static string File(DiffDocument document, int fileIndex)
    {
        var builder = new StringBuilder();
        foreach (var row in document.Rows)
        {
            if (row.FileIndex != fileIndex)
            {
                continue;
            }

            builder.Append(Line(row)).Append('\n');
        }

        return builder.ToString();
    }
}
