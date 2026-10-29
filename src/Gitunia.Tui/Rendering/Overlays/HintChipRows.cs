using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// The chip rows of the editor overlays: the chips flow left to right and start a new row where the next one would
/// not fit, so none is cut off; the same columns decide what is drawn and what a click hits.
/// </summary>
public static class HintChipRows
{
    /// <summary>The frame column of the first chip (border and one column of margin).</summary>
    public const int StartColumn = 2;

    private const int Gap = 2;
    private const string GapText = "  ";

    /// <summary>The chips split into rows for a frame of <paramref name="frameWidth"/> columns.</summary>
    public static List<List<HintChip>> Flow(IEnumerable<HintChip> chips, int frameWidth)
    {
        var available = OverlayBox.InnerWidth(frameWidth) - 1;
        var rows = new List<List<HintChip>> { new() };
        var used = 0;
        foreach (var chip in chips)
        {
            var width = TextWidth.Of(chip.Text);
            var needed = rows[^1].Count == 0 ? width : used + Gap + width;
            if (rows[^1].Count > 0 && needed > available)
            {
                rows.Add([]);
                needed = width;
            }

            rows[^1].Add(chip);
            used = needed;
        }

        return rows;
    }

    /// <summary>One frame row of chips.</summary>
    public static StyledLine Line(IReadOnlyList<HintChip> chips, int frameWidth, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(OverlayBox.InnerWidth(frameWidth), glyphs.Ellipsis)
            .Append(" ", SegmentRole.Status);
        for (var index = 0; index < chips.Count; index++)
        {
            builder.Append(index == 0 ? string.Empty : GapText, SegmentRole.Status);
            HintBar.AppendOne(builder, new HintItem(chips[index].Keys, chips[index].Description));
        }

        return OverlayBox.Wrap(builder.Build(SegmentRole.Status), glyphs);
    }

    /// <summary>The mouse view of the chip at <paramref name="frameColumn"/> of a chip row, null when none.</summary>
    public static string? ViewAt(IReadOnlyList<HintChip> row, int frameColumn)
    {
        var start = StartColumn;
        foreach (var chip in row)
        {
            var end = start + TextWidth.Of(chip.Text);
            if (frameColumn >= start && frameColumn < end)
            {
                return chip.View;
            }

            start = end + Gap;
        }

        return null;
    }
}
