using System.Text;
using Gitunia.Core.Mouse;
using Gitunia.Core.Text;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>
/// The chips at the right edge of the first line of the change group under the cursor in the working-tree diff:
/// <c>stage</c> and <c>discard</c> on the Unstaged side, <c>unstage</c> on the Staged side. They are the same buttons,
/// views and actions as the ones in the title row (<see cref="DiffTitleButtons"/>), placed where the eye is.
/// A row too narrow to keep <see cref="MinimumTextWidth"/>
/// columns of diff text drops <c>discard</c>, then the rest.
/// </summary>
public static class ChangeGroupButtons
{
    /// <summary>Columns of the diff line that stay free of buttons.</summary>
    public const int MinimumTextWidth = 30;

    private static readonly TitleButton Stage = KeyedChip.Plain(DefaultMouseMap.DiffStageButton, "stage");

    private static readonly TitleButton Unstage = KeyedChip.Plain(DefaultMouseMap.DiffUnstageButton, "unstage");

    private static readonly TitleButton Discard = KeyedChip.Plain(DefaultMouseMap.DiffDiscardButton, "discard");

    /// <summary>The buttons that fit, left to right, a space before each, ending at the text's right end.</summary>
    /// <param name="textWidth">Columns of the diff text between the paddings.</param>
    /// <param name="padding">Padding columns on each side of the text; columns count from the line's start.</param>
    /// <param name="side">Which side of the working tree the diff shows.</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap. Keys are shown when all the
    /// buttons that fit also fit with them, otherwise every button is label-only.</param>
    public static IReadOnlyList<TitleButton> Layout(
        int textWidth,
        int padding,
        WorkingTreeButtons side,
        ChipKeys? keys = null)
    {
        TitleButton[] wanted = side switch
        {
            WorkingTreeButtons.Unstaged => [Stage, Discard],
            WorkingTreeButtons.Staged => [Unstage],
            _ => [],
        };
        var chosen = new List<TitleButton>(wanted.Length);
        var used = 0;
        foreach (var button in wanted)
        {
            // One column of space before each button.
            var needed = button.Width + 1;
            if (textWidth - used - needed < MinimumTextWidth)
            {
                break;
            }

            chosen.Add(button);
            used += needed;
        }

        var keyView = DiffTitleButtons.KeyViewOf(side);
        var lookup = keys ?? ChipKeys.Default;
        var keyed = chosen
            .Select(button => KeyedChip.Keyed(button.View, button.Label, lookup.For(button.View, keyView)))
            .ToList();
        var keyedUsed = keyed.Sum(button => button.Width + 1);
        if (chosen.Count > 0 && textWidth - keyedUsed >= MinimumTextWidth)
        {
            chosen = keyed;
            used = keyedUsed;
        }

        var column = padding + textWidth - used + 1;
        var placed = new List<TitleButton>(chosen.Count);
        foreach (var button in chosen)
        {
            placed.Add(button with { Start = column });
            column += button.Width + 1;
        }

        return placed;
    }

    /// <summary>The line with <paramref name="buttons"/> drawn over its right end (the text under is cut).</summary>
    /// <param name="line">The padded line.</param>
    /// <param name="buttons">From <see cref="Layout"/>; none returns the line as it is.</param>
    /// <param name="padding">Padding columns on the right of the line.</param>
    /// <param name="glyphs">Tells whether the ASCII tier is on.</param>
    public static StyledLine Overlay(
        StyledLine line,
        IReadOnlyList<TitleButton> buttons,
        int padding,
        GraphGlyphSet glyphs)
    {
        if (buttons.Count == 0)
        {
            return line;
        }

        var segments = Cut(line.Segments, buttons[0].Start - 1);
        for (var index = 0; index < buttons.Count; index++)
        {
            segments.Add(new StyledSegment(" ", SegmentRole.Plain));
            segments.AddRange(KeyedChip.Segments(buttons[index], glyphs));
        }

        segments.Add(new StyledSegment(new string(' ', padding), SegmentRole.Plain));
        return line with { Segments = segments };
    }

    // The segments cut to the first `columns` columns; a wide character that would straddle the cut becomes a space.
    private static List<StyledSegment> Cut(IReadOnlyList<StyledSegment> segments, int columns)
    {
        var result = new List<StyledSegment>(segments.Count + 1);
        var used = 0;
        foreach (var segment in segments)
        {
            if (used >= columns)
            {
                break;
            }

            var builder = new StringBuilder();
            var counter = new WidthCounter();
            foreach (var rune in segment.Text.EnumerateRunes())
            {
                var width = counter.Next(rune);
                if (used + width > columns)
                {
                    builder.Append(' ', columns - used);
                    used = columns;
                    break;
                }

                builder.Append(rune.ToString());
                used += width;
            }

            result.Add(segment with { Text = builder.ToString() });
        }

        if (used < columns)
        {
            result.Add(new StyledSegment(new string(' ', columns - used), SegmentRole.Plain));
        }

        return result;
    }
}
