using Gitunia.Core.Diff;
using Gitunia.Git.Blobs;
using Gitunia.Git.Blobs.Models;
using Gitunia.Tui.Rendering.Details;

namespace Gitunia.Tui.Loading.Trees;

/// <summary>
/// Builds the document the diff pane shows for the content of one file (Files tab): the lines as content rows
/// numbered on the new side, so scrolling, search and copy work as in a diff. A binary or oversized file, an empty one
/// and a load in progress are a single note row.
/// </summary>
public static class FileContentDocument
{
    /// <summary>The document for a loaded blob.</summary>
    public static DiffDocument From(string path, BlobContent blob)
    {
        switch (blob.Kind)
        {
            case BlobKind.Binary:
                return Message(path, $"Binary file ({FileSizeText.Of(blob.Size)}): its content is not shown");
            case BlobKind.TooLarge:
                var limit = FileSizeText.Of(BlobReader.MaximumBytes);

                return Message(path, $"File too large to show ({FileSizeText.Of(blob.Size)}, the limit is {limit})");
        }

        if (blob.Text.Length == 0)
        {
            return Message(path, "Empty file");
        }

        var lines = Lines(blob.Text);
        var rows = new List<DiffRow>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            rows.Add(new DiffRow(DiffRowKind.Content, lines[index], null, index + 1, 0));
        }

        return new DiffDocument(rows, [], [0], [[path]], lines.Count, path);
    }

    /// <summary>A document of one note row for the file at <paramref name="path"/>.</summary>
    public static DiffDocument Message(string path, string text) =>
        new([new DiffRow(DiffRowKind.Note, text, null, null, 0)], [], [0], [[path]], 0, path);

    /// <summary>
    /// The document of the Files tab's pane while no file is under the cursor: one note row and an empty content path,
    /// so the pane reads as file content (title, hints, no hunks) without naming a file.
    /// </summary>
    public static DiffDocument Placeholder(string text) =>
        new([new DiffRow(DiffRowKind.Note, text, null, null, 0)], [], [0], [[string.Empty]], 0, string.Empty);

    /// <summary>The text of the file, one line per row, for the clipboard.</summary>
    public static string Text(DiffDocument document) =>
        string.Concat(document.Rows.Where(row => row.Kind == DiffRowKind.Content).Select(row => row.Text + "\n"));

    // Lines end at LF; a CR before it (a CRLF file) is dropped so it never reaches the terminal. The empty piece
    // after a final LF is not a line.
    private static List<string> Lines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            var next = end < 0 ? text.Length : end + 1;
            var stop = end < 0 ? text.Length : end;
            if (stop > start && text[stop - 1] == '\r')
            {
                stop--;
            }

            lines.Add(text[start..stop]);
            start = next;
        }

        return lines;
    }
}
