using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>
/// The buttons at the right end of the diff pane's title row: previous and next hunk, fewer and more context, whole file,
/// ignore whitespace, side by side, word wrap and zoom, drawn as
/// padded chips like the tabs (the ASCII tier uses plain arrows and brackets, see <see cref="ButtonLabel"/>). They are the
/// mouse path of the keys <see cref="DefaultMouseMap"/> names, shown before each label (#536).
/// Narrow panes show only the
/// most useful ones (zoom, wrap and the hunk buttons last to go) (the title keeps at least <see cref="MinimumTitleWidth"/> columns). The row is the top edge of the pane's box:
/// a column of line follows each button.
/// </summary>
public static class DiffTitleButtons
{
    /// <summary>Columns of the title text that are never given to buttons.</summary>
    public const int MinimumTitleWidth = 30;

    private static readonly ButtonSpecification Stage = new(DefaultMouseMap.DiffStageButton, " stage ");

    private static readonly ButtonSpecification Unstage = new(DefaultMouseMap.DiffUnstageButton, " unstage ");

    private static readonly ButtonSpecification Discard = new(DefaultMouseMap.DiffDiscardButton, " discard ");

    // Most useful first: when the row is narrow the later ones are dropped.
    private static readonly ButtonSpecification[] Candidates =
    [
        new(DefaultMouseMap.DiffZoomButton, " zoom "),
        new(DefaultMouseMap.DiffWrapButton, " wrap "),
        new(DefaultMouseMap.DiffNextHunkButton, " hunk › "),
        new(DefaultMouseMap.DiffPreviousHunkButton, " ‹ hunk "),
        new(DefaultMouseMap.DiffSideBySideButton, " split "),
        new(DefaultMouseMap.DiffIgnoreWhitespaceButton, " -w "),
        new(DefaultMouseMap.DiffWholeFileButton, " file "),
        new(DefaultMouseMap.DiffMoreContextButton, " ctx+ "),
        new(DefaultMouseMap.DiffLessContextButton, " ctx- "),
    ];

    // Left to right on the row.
    private static readonly string[] DisplayOrder =
    [
        DefaultMouseMap.DiffStageButton,
        DefaultMouseMap.DiffUnstageButton,
        DefaultMouseMap.DiffDiscardButton,
        DefaultMouseMap.DiffPreviousHunkButton,
        DefaultMouseMap.DiffNextHunkButton,
        DefaultMouseMap.DiffLessContextButton,
        DefaultMouseMap.DiffMoreContextButton,
        DefaultMouseMap.DiffWholeFileButton,
        DefaultMouseMap.DiffIgnoreWhitespaceButton,
        DefaultMouseMap.DiffSideBySideButton,
        DefaultMouseMap.DiffWrapButton,
        DefaultMouseMap.DiffZoomButton,
    ];

    /// <summary>The buttons that fit a title row of <paramref name="width"/> columns, left to right, one column of line between them.</summary>
    /// <param name="width">Columns of the title row.</param>
    /// <param name="keys">Where the key text of each button comes from; null uses the default keymap.</param>
    public static IReadOnlyList<TitleButton> Layout(int width, ChipKeys? keys = null) =>
        Layout(width, WorkingTreeButtons.None, keys);

    /// <summary>
    /// The buttons of the working-tree diff: the staging ones come first in the drop order for the stage side (<c>stage</c>
    /// and <c>discard</c> on the Unstaged side, <c>unstage</c> on the Staged side), so a narrow row keeps what the pane is for.
    /// Each button shows its key before the label (<c>[c ‹ hunk</c>) when the buttons that fit also fit with their
    /// keys; otherwise the key text goes first, then the buttons in the drop order.
    /// </summary>
    /// <param name="width">Columns of the title row.</param>
    /// <param name="staging">Which side of the working tree the diff shows.</param>
    /// <param name="keys">Where the key text of each button comes from; null uses the default keymap.</param>
    public static IReadOnlyList<TitleButton> Layout(int width, WorkingTreeButtons staging, ChipKeys? keys = null)
    {
        var chosen = new List<ButtonSpecification>();
        var used = 0;
        foreach (var candidate in Prioritized(staging))
        {
            var needed = TextWidth.Of(candidate.Label) + (chosen.Count > 0 ? 1 : 0);
            if (width - 2 - used - needed < MinimumTitleWidth)
            {
                break;
            }

            chosen.Add(candidate);
            used += needed;
        }

        var keyView = KeyViewOf(staging);
        var lookup = keys ?? ChipKeys.Default;
        var chips = chosen.Select(button => WithKey(button, lookup.For(button.View, keyView))).ToList();
        var keyedUsed = chips.Sum(chip => chip.Width) + Math.Max(0, chips.Count - 1);
        var showKeys = width - 2 - keyedUsed >= MinimumTitleWidth;
        var buttons = new List<TitleButton>(chosen.Count);
        var column = width - 2 - (showKeys ? keyedUsed : used);
        foreach (var chip in chips.OrderBy(candidate => Array.IndexOf(DisplayOrder, candidate.View)))
        {
            var button = showKeys ? chip : chip with { Label = PlainLabel(chosen, chip.View), KeyLength = 0 };
            buttons.Add(button with { Start = column });
            column += button.Width + 1;
        }

        return buttons;
    }

    private static string KeyViewOf(WorkingTreeButtons staging) => staging switch
    {
        WorkingTreeButtons.Unstaged => DefaultKeymap.WorkingUnstagedDiffView,
        WorkingTreeButtons.Staged => DefaultKeymap.WorkingStagedDiffView,
        _ => DefaultKeymap.DiffView,
    };

    private static TitleButton WithKey(ButtonSpecification button, string? key) => key is null
        ? new TitleButton(button.View, button.Label, 0)
        : new TitleButton(button.View, $" {key} {button.Label.Trim()} ", 0, key.Length);

    private static string PlainLabel(List<ButtonSpecification> chosen, string view) =>
        chosen.First(button => button.View == view).Label;

    private static IEnumerable<ButtonSpecification> Prioritized(WorkingTreeButtons staging)
    {
        if (staging == WorkingTreeButtons.Unstaged)
        {
            yield return Stage;
        }
        else if (staging == WorkingTreeButtons.Staged)
        {
            yield return Unstage;
        }

        foreach (var candidate in Candidates)
        {
            yield return candidate;
            if (staging == WorkingTreeButtons.Unstaged && candidate.View == DefaultMouseMap.DiffPreviousHunkButton)
            {
                yield return Discard;
            }
        }
    }
}
