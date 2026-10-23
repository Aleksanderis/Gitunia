using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Files;

/// <summary>
/// The path part of a file row (after the change mark) in either <see cref="FilePathStyle"/>. Name first draws the
/// file name,
/// then the directory dimmed; when the row is too narrow the directory is cut from the left (<c>…ools/Rendering</c>)
/// and
/// dropped when not even that fits, so the name is the last thing to lose columns. A rename shows the old name or path
/// after the directory when all of it fits.
/// </summary>
public static class PathRowLayout
{
    private const char Separator = '/';
    private const string DirectoryGap = "  ";
    private const string FromArrow = " <- ";
    private const string FullPathArrow = " -> ";

    // The ellipsis and at least one character of the directory.
    private const int MinimumDirectoryColumns = 2;

    /// <summary>The pieces of the path that fit <paramref name="columns"/> (the full style is cut by the line
    /// builder, not here).</summary>
    /// <param name="style">The style to lay out.</param>
    /// <param name="path">The path of the file (the new path of a rename), with <c>/</c> separators as git prints
    /// them.</param>
    /// <param name="oldPath">The old path of a rename or copy, null for any other change.</param>
    /// <param name="columns">Columns left for the path.</param>
    /// <param name="ellipsis">The ellipsis that leads a directory cut from the left.</param>
    public static IReadOnlyList<StyledSegment> Segments(
        FilePathStyle style,
        string path,
        string? oldPath,
        int columns,
        string ellipsis)
    {
        if (style == FilePathStyle.FullPath)
        {
            return [new StyledSegment(FullText(path, oldPath), SegmentRole.Plain)];
        }

        // A folder (an untracked directory, a submodule) ends with a separator that belongs to its name.
        var cut = path.TrimEnd(Separator).LastIndexOf(Separator);
        var directory = cut < 0 ? string.Empty : path[..cut];
        var name = path[(cut + 1)..];
        var segments = new List<StyledSegment> { new(name, SegmentRole.FileName) };
        var remaining = columns - TextWidth.Of(name);
        var isDirectoryWhole = true;
        if (directory.Length > 0)
        {
            var room = remaining - DirectoryGap.Length;
            var fitsWhole = TextWidth.Of(directory) <= room;
            var shown = fitsWhole || room >= MinimumDirectoryColumns
                ? TextWidth.FitStart(directory, room, ellipsis)
                : string.Empty;
            if (shown == ellipsis)
            {
                shown = string.Empty;
            }

            isDirectoryWhole = shown == directory;
            if (shown.Length > 0)
            {
                segments.Add(new StyledSegment($"{DirectoryGap}{shown}", SegmentRole.Faint));
                remaining -= DirectoryGap.Length + TextWidth.Of(shown);
            }
        }

        if (oldPath is not null && isDirectoryWhole)
        {
            var from = $"{FromArrow}{OldText(directory, oldPath)}";
            if (TextWidth.Of(from) <= remaining)
            {
                segments.Add(new StyledSegment(from, SegmentRole.Faint));
            }
        }

        return segments;
    }

    /// <summary>The text of the path as the full style shows it: <c>old -&gt; new</c> for a rename, else the
    /// path.</summary>
    public static string FullText(string path, string? oldPath) =>
        oldPath is null ? path : $"{oldPath}{FullPathArrow}{path}";

    /// <summary>The text of the row as <see cref="Segments"/> lays it out, for the quick jump to search and
    /// label.</summary>
    public static string Text(FilePathStyle style, string path, string? oldPath, int columns, string ellipsis) =>
        string.Concat(Segments(style, path, oldPath, columns, ellipsis).Select(segment => segment.Text));

    // A rename inside the same directory shows just the old name, one into another directory the whole old path.
    private static string OldText(string directory, string oldPath)
    {
        var cut = oldPath.LastIndexOf(Separator);
        var oldDirectory = cut < 0 ? string.Empty : oldPath[..cut];

        return oldDirectory == directory ? oldPath[(cut + 1)..] : oldPath;
    }
}
