using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Picker;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>
/// The chips of the picker's footer row: clone and init (the mouse path of <c>Alt-c</c> and <c>Alt-i</c>), then pin or
/// unpin and remove for the highlighted row (<c>Alt-p</c>, <c>Alt-d</c>). A chip that does not fit is left out (the key
/// stays); locating a missing repository has no clone and init, and a directory row has no pin.
/// </summary>
public static class PickerFooterButtons
{
    /// <summary>The columns before the first chip.</summary>
    public const int LeftMargin = 1;

    /// <summary>The chips on a footer of <paramref name="innerWidth"/> columns (inside the side rules), left to right.</summary>
    /// <param name="row">The highlighted row, or null.</param>
    /// <param name="innerWidth">The columns between the side rules.</param>
    /// <param name="isLocating">Whether the picker locates a missing repository.</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap.</param>
    public static IReadOnlyList<TitleButton> Layout(PickerRow? row, int innerWidth, bool isLocating, ChipKeys? keys = null)
    {
        var lookup = keys ?? ChipKeys.Default;
        var specifications = new List<(string View, string Label)>();
        if (!isLocating)
        {
            specifications.Add((DefaultMouseMap.PickerCloneButton, "clone"));
            specifications.Add((DefaultMouseMap.PickerInitButton, "init"));
        }

        if (row is not null && !row.Tags.HasFlag(PickerTags.Directory))
        {
            specifications.Add((DefaultMouseMap.PickerPinButton, row.IsPinned ? "unpin" : "pin"));
            if (row.IsRecent)
            {
                specifications.Add((DefaultMouseMap.PickerRemoveButton, "remove"));
            }
        }

        var buttons = new List<TitleButton>(specifications.Count);
        var column = LeftMargin;
        foreach (var (view, label) in specifications)
        {
            var chip = KeyedChip.Keyed(view, label, lookup.For(view, DefaultKeymap.PickerView, KeyMode.Insert));
            if (column + chip.Width > innerWidth)
            {
                break;
            }

            buttons.Add(chip with { Start = column });
            column += chip.Width + 1;
        }

        return buttons;
    }
}
