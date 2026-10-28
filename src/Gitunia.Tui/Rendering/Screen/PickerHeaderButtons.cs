using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>
/// The chips at the right end of the picker's hint line: clone and init, the mouse path of <c>Alt-c</c> and
/// <c>Alt-i</c>. Narrow screens show none (the hint text matters more); locating a missing repository has none.
/// </summary>
public static class PickerHeaderButtons
{
    /// <summary>The hint line is this row of the picker.</summary>
    public const int Row = 1;

    /// <summary>Screens narrower than this show no buttons; the hint text needs the room before.</summary>
    public const int MinimumWidth = 94;

    /// <summary>
    /// The chips on a line of <paramref name="width"/> columns, left to right (key first, then the action), with a
    /// space after the last.
    /// </summary>
    /// <param name="width">The columns of the line.</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap.</param>
    public static IReadOnlyList<TitleButton> Layout(int width, ChipKeys? keys = null)
    {
        if (width < MinimumWidth)
        {
            return [];
        }

        var lookup = keys ?? ChipKeys.Default;
        var clone = KeyedChip.Keyed(
            DefaultMouseMap.PickerCloneButton,
            "clone",
            lookup.For(DefaultMouseMap.PickerCloneButton, DefaultKeymap.PickerView, KeyMode.Insert));
        var init = KeyedChip.Keyed(
            DefaultMouseMap.PickerInitButton,
            "init",
            lookup.For(DefaultMouseMap.PickerInitButton, DefaultKeymap.PickerView, KeyMode.Insert));
        var start = width - clone.Width - init.Width - 2;
        return [clone with { Start = start }, init with { Start = start + clone.Width + 1 }];
    }
}
