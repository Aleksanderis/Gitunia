using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Picker;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>
/// The chips at the right end of the highlighted picker row: pin or unpin and remove from recents, the mouse path of
/// <c>Alt-p</c> and <c>Alt-d</c>. Directories have none; narrow screens show none (the row text matters more).
/// </summary>
public static class PickerRowButtons
{
    /// <summary>Screens narrower than this show no buttons.</summary>
    public const int MinimumWidth = 72;

    /// <summary>
    /// The chips of <paramref name="row"/> on a line of <paramref name="width"/> columns, left to right, with a space
    /// after the last.
    /// </summary>
    /// <param name="row">The highlighted row.</param>
    /// <param name="width">The columns of the line.</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap.</param>
    public static IReadOnlyList<TitleButton> Layout(PickerRow row, int width, ChipKeys? keys = null)
    {
        if (width < MinimumWidth || row.Tags.HasFlag(PickerTags.Directory))
        {
            return [];
        }

        var lookup = keys ?? ChipKeys.Default;
        var pinLabel = row.IsPinned ? "unpin" : "pin";
        var specifications = new List<ButtonSpecification> { new(DefaultMouseMap.PickerPinButton, pinLabel) };
        if (row.IsRecent)
        {
            specifications.Add(new ButtonSpecification(DefaultMouseMap.PickerRemoveButton, "remove"));
        }

        var chips = specifications
            .Select(specification => KeyedChip.Keyed(
                specification.View,
                specification.Label,
                lookup.For(specification.View, DefaultKeymap.PickerView, KeyMode.Insert)))
            .ToList();
        var column = width - chips.Sum(chip => chip.Width) - chips.Count;
        var buttons = new List<TitleButton>(chips.Count);
        foreach (var chip in chips)
        {
            buttons.Add(chip with { Start = column });
            column += chip.Width + 1;
        }

        return buttons;
    }
}
