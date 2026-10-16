using System.Text;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>
/// The chips at the right edge of the first line of the change group under the cursor in the working-tree diff:
/// <c>stage</c> and <c>discard</c> on the Unstaged side, <c>unstage</c> on the Staged side. They are the same buttons,
/// views and actions as the ones in the title row (<see cref="DiffTitleButtons"/>), placed where the eye is. A row too narrow to keep
/// <see cref="MinimumTextWidth"/> columns of diff text drops <c>discard</c>, then the rest.
/// </summary>
public static class ChangeGroupButtons
{
    /// <summary>Columns of the diff line that stay free of buttons.</summary>
    public const int MinimumTextWidth = 30;

    private static readonly TitleButton Stage = new(DefaultMouseMap.DiffStageButton, " stage ", 0);

    private static readonly TitleButton Unstage = new(DefaultMouseMap.DiffUnstageButton, " unstage ", 0);

    private static readonly TitleButton Discard = new(DefaultMouseMap.DiffDiscardButton, " discard ", 0);

    /// <summary>The buttons that fit, left to right, a column of space before each, ending at the right end of the text.</summary>
    /// <param name="textWidth">Columns of the diff text between the paddings.</param>
    /// <param name="padding">Padding columns on each side of the text; columns count from the start of the padded line.</param>
    /// <param name="side">Which side of the working tree the diff shows.</param>
    public static IReadOnlyList<TitleButton> Layout(int textWidth, int padding, WorkingTreeButtons side)
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

        var column = padding + textWidth - used + 1;
        var placed = new List<TitleButton>(chosen.Count);
        foreach (var button in chosen)
        {
            placed.Add(button with { Start = column });
            column += button.Width + 1;
        }

        return placed;
    }

    /// <summary>The line with <paramref name="buttons"/> drawn over its right end (the text under them is cut).</summary>
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
            var label = ButtonLabel.For(buttons[index].Label, glyphs.IsAscii);
            segments.Add(new StyledSegment(label, SegmentRole.RepositoryTab));
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
            foreach (var rune in segment.Text.EnumerateRunes())
            {
                var width = TextWidth.Of(rune);
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
