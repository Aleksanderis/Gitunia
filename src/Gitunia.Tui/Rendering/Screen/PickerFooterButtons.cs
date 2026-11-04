using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Picker;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>
/// The chips of the picker's footer row. Left: open (<c>Enter</c>), then pin or unpin and remove for the highlighted
/// row (<c>Alt-p</c>, <c>Alt-d</c>). Right: clone and init (<c>Alt-c</c>, <c>Alt-i</c>) and close (<c>Esc</c>). A chip
/// that does not fit is left out (the key stays), the right group first from its left end and the left group before
/// the close chip; locating a missing repository has no clone and init, and a directory row has no pin.
/// </summary>
public static class PickerFooterButtons
{
    /// <summary>The columns before the first chip and after the last.</summary>
    public const int Margin = 1;

    /// <summary>The chips on a footer of <paramref name="innerWidth"/> columns (inside the side rules), left to right.</summary>
    /// <param name="row">The highlighted row, or null.</param>
    /// <param name="innerWidth">The columns between the side rules.</param>
    /// <param name="isLocating">Whether the picker locates a missing repository.</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap.</param>
    /// <param name="acceptLabel">The label of the first chip, "open" when null.</param>
    public static IReadOnlyList<TitleButton> Layout(
        PickerRow? row,
        int innerWidth,
        bool isLocating,
        ChipKeys? keys = null,
        string? acceptLabel = null)
    {
        var lookup = keys ?? ChipKeys.Default;
        var buttons = new List<TitleButton>();
        var right = RightChips(lookup, isLocating);
        var column = PlaceLeft(buttons, LeftChips(lookup, row, acceptLabel ?? "open"), innerWidth - right[^1].Width - 1);
        PlaceRight(buttons, right, column, innerWidth);
        return buttons;
    }

    /// <summary>The label of the first chip for the highlighted line: "expand" or "collapse" on a more or less line,
    /// else "open".</summary>
    /// <param name="picker">The picker.</param>
    /// <param name="visible">The listed lines.</param>
    public static string AcceptLabel(PickerState picker, IReadOnlyList<PickerListEntry> visible)
    {
        var position = PickerListPlan.PositionOf(visible, picker);
        if (position < 0)
        {
            return "open";
        }

        return visible[position].Kind switch
        {
            PickerEntryKind.More => "expand",
            PickerEntryKind.Less => "collapse",
            _ => "open",
        };
    }

    private static List<TitleButton> LeftChips(ChipKeys keys, PickerRow? row, string acceptLabel)
    {
        var chips = new List<TitleButton> { Chip(keys, DefaultMouseMap.PickerAcceptButton, acceptLabel) };
        if (row is null || row.Tags.HasFlag(PickerTags.Directory))
        {
            return chips;
        }

        chips.Add(Chip(keys, DefaultMouseMap.PickerPinButton, row.IsPinned ? "unpin" : "pin"));
        if (row.IsRecent)
        {
            chips.Add(Chip(keys, DefaultMouseMap.PickerRemoveButton, "remove"));
        }

        return chips;
    }

    private static List<TitleButton> RightChips(ChipKeys keys, bool isLocating)
    {
        var chips = new List<TitleButton>();
        if (!isLocating)
        {
            chips.Add(Chip(keys, DefaultMouseMap.PickerCloneButton, "clone"));
            chips.Add(Chip(keys, DefaultMouseMap.PickerInitButton, "init"));
        }

        chips.Add(Chip(keys, DefaultMouseMap.PickerCloseButton, "close"));
        return chips;
    }

    // Returns the first free column after the chips that fit; the close chip's room is kept free.
    private static int PlaceLeft(List<TitleButton> buttons, List<TitleButton> chips, int limit)
    {
        var column = Margin;
        foreach (var chip in chips)
        {
            if (column + chip.Width > limit - Margin)
            {
                break;
            }

            buttons.Add(chip with { Start = column });
            column += chip.Width + 1;
        }

        return column;
    }

    // Fills from the right edge inwards, so a chip that is dropped is the leftmost of the group.
    private static void PlaceRight(List<TitleButton> buttons, List<TitleButton> chips, int firstFree, int innerWidth)
    {
        var placed = new List<TitleButton>(chips.Count);
        var edge = innerWidth - Margin;
        for (var index = chips.Count - 1; index >= 0; index--)
        {
            var start = edge - chips[index].Width;
            if (start < firstFree)
            {
                break;
            }

            placed.Insert(0, chips[index] with { Start = start });
            edge = start - 1;
        }

        buttons.AddRange(placed);
    }

    private static TitleButton Chip(ChipKeys keys, string view, string label) =>
        KeyedChip.Keyed(view, label, keys.For(view, DefaultKeymap.PickerView, KeyMode.Insert));
}
