using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Tabs;

/// <summary>
/// The fetch, pull and push chips left of the commands button on the tab row, the mouse path of <c>&lt;space&gt;rf</c>,
/// <c>&lt;space&gt;rl</c> and <c>&lt;space&gt;rp</c> (#456). While one of them runs they are replaced by one cancel chip
/// (the mouse path of <c>&lt;space&gt;rc</c>). Each chip shows its key from the keymap before its label
/// (<c>&lt;space&gt;rf fetch</c>, #536) when the row is wide enough for that (<see cref="IsKeyed"/>); a narrower row
/// drops the key text first and rows narrower than <see cref="MinimumWidth"/> have no chips at all (the keys and the
/// command palette stay). Pure layout: the same width gives the same columns, busy or not, so the tabs never jump.
/// </summary>
public static class RemoteChips
{
    /// <summary>Rows narrower than this show no chips.</summary>
    public const int MinimumWidth = 100;

    private const int GapColumns = 1;

    private static readonly ButtonSpecification[] IdleChips =
    [
        new(DefaultMouseMap.RemoteFetchButton, "fetch"),
        new(DefaultMouseMap.RemotePullButton, "pull"),
        new(DefaultMouseMap.RemotePushButton, "push"),
    ];

    private static readonly ButtonSpecification CancelChip = new(DefaultMouseMap.RemoteCancelButton, "cancel");

    /// <summary>The idle chips, in order.</summary>
    /// <param name="keyed">Whether each chip shows its key before its label.</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap.</param>
    public static IReadOnlyList<TitleButton> Idle(bool keyed, ChipKeys? keys = null) =>
        [.. IdleChips.Select(chip => Build(chip, keyed, keys))];

    /// <summary>The one chip shown while a fetch, pull or push of the active tab runs.</summary>
    /// <param name="keyed">Whether the chip shows its key before its label.</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap.</param>
    public static TitleButton Cancel(bool keyed, ChipKeys? keys = null) => Build(CancelChip, keyed, keys);

    /// <summary>The columns the idle chips take with the gaps between them, the most any state needs.</summary>
    /// <param name="keyed">Whether the chips show their keys.</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap.</param>
    public static int ReservedWidth(bool keyed, ChipKeys? keys = null)
    {
        var chips = Idle(keyed, keys);
        return chips.Sum(chip => TextWidth.Of(chip.Label)) + (chips.Count - 1) * GapColumns;
    }

    /// <summary>Whether a row of <paramref name="width"/> columns has room for the chips with their keys.</summary>
    public static bool IsKeyed(int width, ChipKeys? keys = null) =>
        width >= MinimumWidth + ReservedWidth(true, keys) - ReservedWidth(false, keys);

    /// <summary>The first column of the chips in a row of <paramref name="width"/> columns, null if none.</summary>
    public static int? Start(int width, ChipKeys? keys = null)
    {
        if (width >= MinimumWidth && TabBarLayout.CommandsStart(width, keys) is { } commands)
        {
            return commands - GapColumns - ReservedWidth(IsKeyed(width, keys), keys);
        }

        return null;
    }

    /// <summary>The chips shown now in a row of <paramref name="width"/> columns, left to right.</summary>
    public static IReadOnlyList<TitleButton> Shown(int width, bool isBusy, ChipKeys? keys = null)
    {
        var keyed = IsKeyed(width, keys);

        return isBusy ? [Cancel(keyed, keys)] : Idle(keyed, keys);
    }

    /// <summary>The mouse view of the chip at <paramref name="column"/> of a row, null when it is on no chip.</summary>
    public static string? ViewAt(int width, bool isBusy, int column, ChipKeys? keys = null)
    {
        if (Start(width, keys) is not { } start)
        {
            return null;
        }

        var left = start;
        foreach (var chip in Shown(width, isBusy, keys))
        {
            var chipWidth = TextWidth.Of(chip.Label);
            if (column >= left && column < left + chipWidth)
            {
                return chip.View;
            }

            left += chipWidth + GapColumns;
        }

        return null;
    }

    private static TitleButton Build(ButtonSpecification chip, bool keyed, ChipKeys? keys)
    {
        var key = keyed ? (keys ?? ChipKeys.Default).For(chip.View, KeyBindingTable.GlobalView) : null;

        return KeyedChip.Keyed(chip.View, chip.Label, key);
    }
}
