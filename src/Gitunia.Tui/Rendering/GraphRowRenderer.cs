using Gitunia.Core.Graph;
using Gitunia.Git;

namespace Gitunia.Tui.Rendering;

/// <summary>Draws one graph row: lanes, short hash, refs and subject, cut to the width.</summary>
public static class GraphRowRenderer
{
    private const int HashLength = 7;

    public static StyledLine Render(GraphEntry entry, GraphRowOptions options, bool isSelected)
    {
        var builder = new LineBuilder(options.Width, options.Glyphs.Ellipsis);
        AppendLanes(builder, LaneCellBuilder.Build(entry.Row, options.LaneColumns, options.Glyphs));
        builder.Append(" ", SegmentRole.Plain);
        builder.Append(entry.Id.ToString()[..HashLength], SegmentRole.Hash);
        builder.Append(" ", SegmentRole.Plain);
        AppendDecorations(builder, entry.Decorations);
        builder.Append(entry.Subject, SegmentRole.Plain);
        return builder.Build(SegmentRole.Plain, isSelected);
    }

    /// <summary>A row without a commit: the slot reserved for the working-tree pseudo-row, and rows past the loaded end.</summary>
    public static StyledLine RenderEmpty(GraphRowOptions options, bool isSelected) =>
        new LineBuilder(options.Width, options.Glyphs.Ellipsis).Build(SegmentRole.Plain, isSelected);

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

    private static void AppendDecorations(LineBuilder builder, IReadOnlyList<CommitDecoration> decorations)
    {
        if (decorations.Count == 0)
        {
            return;
        }

        builder.Append("(", SegmentRole.Punctuation);
        for (var index = 0; index < decorations.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ", SegmentRole.Punctuation);
            }

            var decoration = decorations[index];
            switch (decoration.Kind)
            {
                case RefKind.LocalBranch when decoration.IsCurrent:
                    builder.Append("HEAD -> ", SegmentRole.CurrentBranch).Append(decoration.Name, SegmentRole.CurrentBranch);
                    break;
                case RefKind.LocalBranch:
                    builder.Append(decoration.Name, SegmentRole.LocalBranch);
                    break;
                case RefKind.RemoteBranch:
                    builder.Append(decoration.Name, SegmentRole.RemoteBranch);
                    break;
                case RefKind.Tag:
                    builder.Append("tag: " + decoration.Name, SegmentRole.Tag);
                    break;
                case RefKind.Head:
                    builder.Append("HEAD", SegmentRole.CurrentBranch);
                    break;
                default:
                    builder.Append(decoration.Name, SegmentRole.Faint);
                    break;
            }
        }

        builder.Append(") ", SegmentRole.Punctuation);
    }
}
