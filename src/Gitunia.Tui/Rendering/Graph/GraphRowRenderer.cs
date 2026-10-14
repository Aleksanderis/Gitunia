using System.Globalization;
using Gitunia.Core.Graph;
using Gitunia.Core.Search;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Graph;

/// <summary>Draws one graph row: as a table: lanes, message (refs and subject), author, date and short hash, cut to the width.</summary>
public static class GraphRowRenderer
{
    private const int HashLength = 7;
    private const string DateFormat = "yyyy-MM-dd";

    // Blank columns between a row's own lines and its message.
    private const int MinimumLaneGap = 2;

    private static readonly string CompactGapText = new(' ', GraphColumnLayout.CompactColumnGap);
    private static readonly string SpaciousGapText = new(' ', GraphColumnLayout.SpaciousColumnGap);

    public static StyledLine Render(GraphEntry entry, GraphRowOptions options, bool isSelected)
    {
        var columns = options.Columns;
        var ellipsis = options.Glyphs.Ellipsis;
        var isMatch = options.Highlight is { } query && GraphSearchMatcher.IsMatch(query, entry);
        var builder = new LineBuilder(options.Width, ellipsis);
        var cells = LaneCellBuilder.Build(entry.Row, options.LaneColumns, options.Glyphs);
        var usedLaneWidth = UsedLaneWidth(cells);
        var areaWidth = columns.LaneWidth + 1;
        var gap = Math.Min(MinimumLaneGap, areaWidth - usedLaneWidth);
        AppendLanes(builder, cells[..usedLaneWidth]);
        builder.Append(new string(' ', gap), SegmentRole.Plain);

        // The message starts right after this row's own lines (#217); the columns after it stay where they are, so the message takes the room the lanes left free.
        var message = new LineBuilder(columns.MessageWidth + areaWidth - usedLaneWidth - gap, ellipsis);
        AppendDecorations(message, entry.Decorations, options.Glyphs);
        AppendSubject(message, entry.Subject, isMatch ? options.Highlight : null);
        builder.Append(message.Build());

        if (columns.AuthorWidth > 0)
        {
            var isAuthorMatch = isMatch && options.Highlight!.Field == SearchField.Author;
            AppendColumn(builder, entry.AuthorName, columns.AuthorWidth, isAuthorMatch ? SegmentRole.SearchMatch : SegmentRole.Plain, columns.ColumnGap, ellipsis);
        }

        if (columns.DateWidth > 0)
        {
            AppendColumn(builder, entry.AuthorTime.DateTime.ToString(DateFormat, CultureInfo.InvariantCulture), columns.DateWidth, SegmentRole.Faint, columns.ColumnGap, ellipsis);
        }

        if (columns.HashWidth > 0)
        {
            var isHashMatch = isMatch && options.Highlight!.Field == SearchField.Hash;
            AppendColumn(builder, entry.Id.ToString()[..HashLength], columns.HashWidth, isHashMatch ? SegmentRole.SearchMatch : SegmentRole.Hash, columns.ColumnGap, ellipsis);
        }

        return builder.Build(SegmentRole.Plain, isSelected);
    }

    /// <summary>A row without a commit: the slot reserved for the working-tree pseudo-row, and rows past the loaded end.</summary>
    public static StyledLine RenderEmpty(GraphRowOptions options, bool isSelected) =>
        new LineBuilder(options.Width, options.Glyphs.Ellipsis).Build(SegmentRole.Plain, isSelected);

    // Columns from the left up to the last one that has a line; at least one, for the node.
    private static int UsedLaneWidth(GraphCell[] cells)
    {
        var used = cells.Length;
        while (used > 1 && cells[used - 1].Glyph == ' ')
        {
            used--;
        }

        return used;
    }

    private static string GapText(int gap) => gap == GraphColumnLayout.SpaciousColumnGap ? SpaciousGapText : CompactGapText;

    // One column after a gap: the text clipped to the column and padded so the next column lines up.
    private static void AppendColumn(LineBuilder builder, string text, int width, SegmentRole role, int gap, string ellipsis)
    {
        builder.Append(GapText(gap), SegmentRole.Plain);
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
                    builder.Append($"tag: {label.Name}", SegmentRole.Tag);
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
