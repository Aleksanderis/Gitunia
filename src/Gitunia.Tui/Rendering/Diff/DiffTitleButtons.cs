using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>
/// The buttons at the right end of the diff pane's title row: previous and next file and hunk, word wrap and zoom, drawn as padded
/// chips like the tabs (<c>‹ file</c>, <c>file ›</c>, <c>‹ hunk</c>, <c>hunk ›</c>, <c>wrap</c>, <c>zoom</c>; the ASCII tier uses <c>&lt;</c>, <c>&gt;</c> and brackets). They
/// are the mouse path of <c>[f</c> <c>]f</c> <c>[c</c> <c>]c</c> <c>w</c> and <c>&lt;space&gt;z</c>. Narrow panes show only the
/// last ones (the title keeps at least <see cref="MinimumTitleWidth"/> columns). The row is the top edge of the pane's box: a space and a stretch of line follow the last button.
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
        new(DefaultMouseMap.DiffNextHunkButton, " hunk › "),
        new(DefaultMouseMap.DiffPreviousHunkButton, " ‹ hunk "),
        new(DefaultMouseMap.DiffNextFileButton, " file › "),
        new(DefaultMouseMap.DiffPreviousFileButton, " ‹ file "),
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
