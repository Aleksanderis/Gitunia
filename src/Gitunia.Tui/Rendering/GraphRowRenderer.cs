using System.Globalization;
using Gitunia.Core.Graph;
using Gitunia.Core.Search;
using Gitunia.Git;

namespace Gitunia.Tui.Rendering;

/// <summary>Draws one graph row: as a table: lanes, message (refs and subject), author, date and short hash, cut to the width.</summary>
public static class GraphRowRenderer
{
    private const int HashLength = 7;
    private const string DateFormat = "yyyy-MM-dd";

    public static StyledLine Render(GraphEntry entry, GraphRowOptions options, bool isSelected)
    {
        var columns = options.Columns;
        var ellipsis = options.Glyphs.Ellipsis;
        var isMatch = options.Highlight is { } query && GraphSearchMatcher.IsMatch(query, entry);
        var builder = new LineBuilder(options.Width, ellipsis);
        AppendLanes(builder, LaneCellBuilder.Build(entry.Row, options.LaneColumns, options.Glyphs));
        builder.Append(" ", SegmentRole.Plain);

        var message = new LineBuilder(columns.MessageWidth, ellipsis);
        AppendDecorations(message, entry.Decorations, options.Glyphs);
        AppendSubject(message, entry.Subject, isMatch ? options.Highlight : null);
        builder.Append(message.Build());

        if (columns.AuthorWidth > 0)
        {
            var isAuthorMatch = isMatch && options.Highlight!.Field == SearchField.Author;
            AppendColumn(builder, entry.AuthorName, columns.AuthorWidth, isAuthorMatch ? SegmentRole.SearchMatch : SegmentRole.Plain, ellipsis);
        }

        if (columns.DateWidth > 0)
        {
            AppendColumn(builder, entry.AuthorTime.DateTime.ToString(DateFormat, CultureInfo.InvariantCulture), columns.DateWidth, SegmentRole.Faint, ellipsis);
        }

        if (columns.HashWidth > 0)
        {
            var isHashMatch = isMatch && options.Highlight!.Field == SearchField.Hash;
            AppendColumn(builder, entry.Id.ToString()[..HashLength], columns.HashWidth, isHashMatch ? SegmentRole.SearchMatch : SegmentRole.Hash, ellipsis);
        }

        return builder.Build(SegmentRole.Plain, isSelected);
    }

    /// <summary>A row without a commit: the slot reserved for the working-tree pseudo-row, and rows past the loaded end.</summary>
    public static StyledLine RenderEmpty(GraphRowOptions options, bool isSelected) =>
        new LineBuilder(options.Width, options.Glyphs.Ellipsis).Build(SegmentRole.Plain, isSelected);

    // One column after a space: the text clipped to the column and padded so the next column lines up.
    private static void AppendColumn(LineBuilder builder, string text, int width, SegmentRole role, string ellipsis)
    {
        builder.Append(" ", SegmentRole.Plain);
        builder.Append(new LineBuilder(width, ellipsis).Append(text, role).Build());
    }

    private static void AppendSubject(LineBuilder builder, string subject, SearchQuery? highlight)
    {
        if (highlight is null || !GraphSearchMatcher.TryFindInSubject(highlight, subject, out var start, out var length))
        {
            builder.Append(subject, SegmentRole.Plain);
            return;
        }

        builder.Append(subject[..start], SegmentRole.Plain);
        builder.Append(subject.Substring(start, length), SegmentRole.SearchMatch);
        builder.Append(subject[(start + length)..], SegmentRole.Plain);
    }

    private static void AppendLanes(LineBuilder builder, GraphCell[] cells)
    {
        var start = 0;
        for (var index = 1; index <= cells.Length; index++)
        {
            if (index < cells.Length && cells[index].Lane == cells[start].Lane)
            {
                continue;
            }

            var run = new char[index - start];
            for (var position = 0; position < run.Length; position++)
            {
                run[position] = cells[start + position].Glyph;
            }

            var lane = cells[start].Lane;
            builder.Append(new string(run), lane < 0 ? SegmentRole.Plain : SegmentRole.Lane, Math.Max(lane, 0));
            start = index;
        }
    }

    private static void AppendDecorations(LineBuilder builder, IReadOnlyList<CommitDecoration> decorations, GraphGlyphSet glyphs)
    {
        var labels = RefLabelBuilder.Build(decorations);
        if (labels.Count == 0)
        {
            return;
        }

        builder.Append("(", SegmentRole.Punctuation);
        for (var index = 0; index < labels.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ", SegmentRole.Punctuation);
            }

            var label = labels[index];
            switch (label.Kind)
            {
                case RefKind.LocalBranch when label.IsCurrent:
                    builder.Append(glyphs.CurrentBranch, SegmentRole.CurrentBranch).Append(label.Name, SegmentRole.CurrentBranch);
                    break;
                case RefKind.LocalBranch:
                    builder.Append(label.Name, SegmentRole.LocalBranch);
                    break;
                case RefKind.RemoteBranch:
                    builder.Append(label.Name, SegmentRole.RemoteBranch);
                    break;
                case RefKind.Tag:
                    builder.Append("tag: " + label.Name, SegmentRole.Tag);
                    break;
                case RefKind.Head:
                    builder.Append("HEAD", SegmentRole.CurrentBranch);
                    break;
                default:
                    builder.Append(label.Name, SegmentRole.Faint);
                    break;
            }

            if (label.HasRemote)
            {
                builder.Append(glyphs.Remote, SegmentRole.RemoteBranch);
            }
        }

        builder.Append(") ", SegmentRole.Punctuation);
    }
}
