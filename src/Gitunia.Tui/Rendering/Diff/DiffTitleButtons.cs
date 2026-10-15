using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>
/// The buttons at the right end of the diff pane's title row: previous and next hunk, fewer and more context, whole file,
/// ignore whitespace, side by side, word wrap and zoom, drawn as
/// padded chips like the tabs (the ASCII tier uses plain arrows and brackets, see <see cref="ButtonLabel"/>). They are the
/// mouse path of <c>[c</c> <c>]c</c> <c>-</c> <c>+</c> <c>F</c> <c>W</c> <c>S</c> <c>w</c> and <c>&lt;space&gt;z</c>. Narrow panes show only the
/// last ones (the title keeps at least <see cref="MinimumTitleWidth"/> columns). The row is the top edge of the pane's box:
/// a column of line follows each button.
/// </summary>
public static class DiffTitleButtons
{
    /// <summary>Columns of the title text that are never given to buttons.</summary>
    public const int MinimumTitleWidth = 30;

    // Right to left, in the order they are added: the most useful stay when the row is narrow.
    private static readonly ButtonSpecification[] Candidates =
    [
        new(DefaultMouseMap.DiffZoomButton, " zoom "),
        new(DefaultMouseMap.DiffWrapButton, " wrap "),
        new(DefaultMouseMap.DiffSideBySideButton, " split "),
        new(DefaultMouseMap.DiffIgnoreWhitespaceButton, " -w "),
        new(DefaultMouseMap.DiffWholeFileButton, " file "),
        new(DefaultMouseMap.DiffMoreContextButton, " ctx+ "),
        new(DefaultMouseMap.DiffLessContextButton, " ctx- "),
        new(DefaultMouseMap.DiffNextHunkButton, " hunk › "),
        new(DefaultMouseMap.DiffPreviousHunkButton, " ‹ hunk "),
    ];

    /// <summary>The buttons that fit a title row of <paramref name="width"/> columns, left to right, one column of line between them.</summary>
    public static IReadOnlyList<TitleButton> Layout(int width)
    {
        var chosen = new List<ButtonSpecification>();
        var used = 0;
        foreach (var candidate in Candidates)
        {
            var needed = TextWidth.Of(candidate.Label) + (chosen.Count > 0 ? 1 : 0);
            if (width - 2 - used - needed < MinimumTitleWidth)
            {
                break;
            }

            chosen.Add(candidate);
            used += needed;
        }

        var buttons = new List<TitleButton>(chosen.Count);
        var column = width - 2 - used;
        for (var index = chosen.Count - 1; index >= 0; index--)
        {
            buttons.Add(new TitleButton(chosen[index].View, chosen[index].Label, column));
            column += TextWidth.Of(chosen[index].Label) + 1;
        }

        return buttons;
    }
}
