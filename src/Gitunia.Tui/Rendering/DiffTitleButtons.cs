using Gitunia.Core.Mouse;

namespace Gitunia.Tui.Rendering;

/// <summary>
/// The buttons at the right end of the diff pane's title row: previous and next file and hunk, word wrap, zoom and close. They
/// are the mouse path of <c>[f</c> <c>]f</c> <c>[c</c> <c>]c</c> <c>w</c> <c>&lt;space&gt;z</c> and <c>q</c>. Narrow panes show only the
/// last ones (the title keeps at least <see cref="MinimumTitleWidth"/> columns). The row is the top edge of the pane's box: a space and a stretch of line follow the last button.
/// </summary>
public static class DiffTitleButtons
{
    /// <summary>Columns of the title text that are never given to buttons.</summary>
    public const int MinimumTitleWidth = 30;

    // Right to left, in the order they are added: the most useful stay when the row is narrow.
    private static readonly (string View, string Label)[] Candidates =
    [
        (DefaultMouseMap.DiffCloseButton, "[x]"),
        (DefaultMouseMap.DiffZoomButton, "[z]"),
        (DefaultMouseMap.DiffWrapButton, "[w]"),
        (DefaultMouseMap.DiffNextHunkButton, "[h>]"),
        (DefaultMouseMap.DiffPreviousHunkButton, "[<h]"),
        (DefaultMouseMap.DiffNextFileButton, "[f>]"),
        (DefaultMouseMap.DiffPreviousFileButton, "[<f]"),
    ];

    /// <summary>The buttons that fit a title row of <paramref name="width"/> columns, left to right, one space between them.</summary>
    public static IReadOnlyList<TitleButton> Layout(int width)
    {
        var chosen = new List<(string View, string Label)>();
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
