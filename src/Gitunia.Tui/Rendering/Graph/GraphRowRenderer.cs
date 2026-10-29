using System.Globalization;
using Gitunia.Core.Graph;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Search;
using Gitunia.Core.Search.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Graph;

/// <summary>Draws one graph row: as a table: lanes, message (refs and subject), author, date and short hash, cut to the width.</summary>
public static class GraphRowRenderer
{
    private const int HashLength = 7;
    private const string DateFormat = "yyyy-MM-dd";
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    // Blank columns between a row's own lines and its message.
    private const int MinimumLaneGap = 2;

    // Between two labels and after the last one.
    private const string LabelSeparator = " ";

    /// <summary>Before the labels of the commit marked for a comparison.</summary>
    private const string MarkText = "[mark] ";

    private static readonly string CompactGapText = new(' ', GraphColumnLayout.CompactColumnGap);
    private static readonly string SpaciousGapText = new(' ', GraphColumnLayout.SpaciousColumnGap);

    public static StyledLine Render(GraphEntry entry, GraphRowOptions options, bool isSelected, JumpLabel? jump = null)
    {
        var columns = options.Columns;
        var ellipsis = options.Glyphs.Ellipsis;
        var isMatch = options.Highlight is { } query && GraphSearchMatcher.IsMatch(query, entry);
        var builder = new LineBuilder(options.Width, ellipsis);
        var layout = MessageLayoutOf(entry, options);
        AppendLanes(builder, layout.Cells[..layout.UsedLaneWidth]);
        builder.Append(new string(' ', layout.Gap), SegmentRole.Plain);

        // The message starts right after this row's own lines (#217); the columns after it stay where they are, so the message takes the room the lanes left free.
        var message = new LineBuilder(layout.MessageWidth, ellipsis);
        var decorations = DecorationSegments(entry, options);
        if (jump is { } target)
        {
            JumpOverlay.Append(message, [.. decorations, new StyledSegment(entry.Subject, SegmentRole.Plain)], target);
        }
        else
        {
            AppendAll(message, decorations);
            AppendSubject(message, entry.Subject, isMatch ? options.Highlight : null);
        }

        builder.Append(message.Build());

        if (columns.AuthorWidth > 0)
        {
            var isAuthorMatch = isMatch && options.Highlight!.Field == SearchField.Author;
            AppendColumn(builder, entry.AuthorName, columns.AuthorWidth, isAuthorMatch ? SegmentRole.SearchMatch : SegmentRole.Plain, columns.ColumnGap, ellipsis);
        }

        if (columns.DateWidth > 0)
        {
            var format = columns.DateWidth == GraphColumnLayout.DateOnlyWidth ? DateFormat : DateTimeFormat;
            var date = entry.AuthorTime.DateTime.ToString(format, CultureInfo.InvariantCulture);
            AppendColumn(builder, date, columns.DateWidth, SegmentRole.Faint, columns.ColumnGap, ellipsis);
        }

        if (columns.HashWidth > 0)
        {
            var isHashMatch = isMatch && options.Highlight!.Field == SearchField.Hash;
            AppendColumn(builder, entry.Id.ToString()[..HashLength], columns.HashWidth, isHashMatch ? SegmentRole.SearchMatch : SegmentRole.Hash, columns.ColumnGap, ellipsis);
        }

        return builder.Build(SegmentRole.Plain, isSelected);
    }

    /// <summary>Columns the message of a row (ref labels and subject) can use: what a quick jump label must fit in.</summary>
    /// <param name="entry">The commit.</param>
    /// <param name="options">The layout of the graph rows.</param>
    public static int MessageColumns(GraphEntry entry, GraphRowOptions options) => MessageLayoutOf(entry, options).MessageWidth;

    /// <summary>The message of a row as plain text, ref labels then subject: the text a quick jump matches against.</summary>
    /// <param name="entry">The commit.</param>
    /// <param name="options">The layout of the graph rows.</param>
    public static string MessageText(GraphEntry entry, GraphRowOptions options) =>
        string.Concat(DecorationSegments(entry, options).Select(segment => segment.Text)) + entry.Subject;

    /// <summary>A row without a commit: the slot reserved for the working-tree pseudo-row, and rows past the loaded end.</summary>
    public static StyledLine RenderEmpty(GraphRowOptions options, bool isSelected) =>
        new LineBuilder(options.Width, options.Glyphs.Ellipsis).Build(SegmentRole.Plain, isSelected);

    /// <summary>
    /// The row under the last loaded commit of a log cut at the soft row limit (#555): a faint lane stroke and a
    /// hint that <c>G</c> loads the rest. It holds no commit.
    /// </summary>
    /// <param name="options">The layout of the graph rows.</param>
    /// <param name="isSelected">The cursor is on the row.</param>
    public static StyledLine RenderCutMarker(GraphRowOptions options, bool isSelected)
    {
        var stroke = options.Glyphs.IsAscii ? ":" : "⋮";
        var builder = new LineBuilder(options.Width, options.Glyphs.Ellipsis);
        builder.Append(stroke, SegmentRole.Faint);
        builder.Append(" more commits below, G loads the rest", SegmentRole.Faint);

        return builder.Build(SegmentRole.Plain, isSelected);
    }

    private static GraphMessageLayout MessageLayoutOf(GraphEntry entry, GraphRowOptions options)
    {
        var cells = LaneCellBuilder.Build(entry.Row, options.LaneColumns, options.Glyphs, entry.Decorations.Count > 0);
        var usedLaneWidth = UsedLaneWidth(cells);
        var areaWidth = options.Columns.LaneWidth + 1;
        var gap = Math.Min(MinimumLaneGap, areaWidth - usedLaneWidth);

        return new GraphMessageLayout(cells, usedLaneWidth, gap, options.Columns.MessageWidth + areaWidth - usedLaneWidth - gap);
    }

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
            // A message hit whose text sits in the body only has nothing to underline, so the whole subject is marked.
            var role = highlight?.Field == SearchField.Body ? SegmentRole.SearchMatch : SegmentRole.Plain;
            builder.Append(subject, role);
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
            if (index < cells.Length && cells[index].Color == cells[start].Color)
            {
                continue;
            }

            var run = new char[index - start];
            for (var position = 0; position < run.Length; position++)
            {
                run[position] = cells[start + position].Glyph;
            }

            var color = cells[start].Color;
            builder.Append(new string(run), color < 0 ? SegmentRole.Plain : SegmentRole.Lane, Math.Max(color, 0));
            start = index;
        }
    }

    private static void AppendAll(LineBuilder builder, List<StyledSegment> segments)
    {
        foreach (var segment in segments)
        {
            builder.Append(segment.Text, segment.Role, segment.ColorIndex);
        }
    }

    private static List<StyledSegment> DecorationSegments(GraphEntry entry, GraphRowOptions options)
    {
        var glyphs = options.Glyphs;
        var labels = RefLabelBuilder.Build(entry.Decorations, options.UpstreamGoneBranches);
        var builder = new List<StyledSegment>();
        if (options.MarkedCommit == entry.Id)
        {
            // The text says it too, so the mark does not depend on colour (ux.md, "Comparing two commits").
            builder.Add(new StyledSegment(MarkText, SegmentRole.Tag));
        }

        if (labels.Count == 0)
        {
            return builder;
        }

        for (var index = 0; index < labels.Count; index++)
        {
            if (index > 0)
            {
                builder.Add(new StyledSegment(LabelSeparator, SegmentRole.Punctuation));
            }

            AppendLabel(builder, labels[index], entry.Row.NodeColor, glyphs);
        }

        builder.Add(new StyledSegment(LabelSeparator, SegmentRole.Punctuation));

        return builder;
    }

    // A branch is one box: the HEAD mark and the remote glyph sit inside it, next to the name (#804).
    private static void AppendLabel(List<StyledSegment> builder, RefLabel label, int colorIndex, GraphGlyphSet glyphs)
    {
        switch (label.Kind)
        {
            case RefKind.LocalBranch or RefKind.RemoteBranch:
                var prefix = label is { Kind: RefKind.LocalBranch, IsCurrent: true } ? glyphs.CurrentBranch : "";
                var suffix = label.HasRemote ? glyphs.Remote : "";
                builder.Add(new StyledSegment($"{prefix}{label.Name}{suffix}", SegmentRole.LaneLabel, colorIndex));
                break;
            case RefKind.Tag:
                builder.Add(new StyledSegment($"tag: {label.Name}", SegmentRole.Tag));
                break;
            case RefKind.Head:
                builder.Add(new StyledSegment("HEAD", SegmentRole.CurrentBranch));
                break;
            default:
                builder.Add(new StyledSegment(label.Name, SegmentRole.Faint));
                break;
        }

        if (label.IsUpstreamGone)
        {
            builder.Add(new StyledSegment(glyphs.UpstreamGone, SegmentRole.Faint));
        }
    }
}
