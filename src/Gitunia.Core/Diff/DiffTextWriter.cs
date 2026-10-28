using System.Text;

namespace Gitunia.Core.Diff;

/// <summary>Turns diff rows back into plain text, without markers, for the clipboard.</summary>
public static class DiffTextWriter
{
    /// <summary>One row as plain text for the clipboard: no <c>+</c>, <c>-</c> or space marker (#756).</summary>
    public static string Line(DiffRow row) => row.Text;

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
