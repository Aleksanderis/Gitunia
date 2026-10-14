using Gitunia.Core.Mouse;
using Gitunia.Core.Picker;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>
/// The buttons at the right end of the highlighted picker row: pin or unpin and remove from recents, the mouse path of
/// <c>Alt-p</c> and <c>Alt-d</c>. Directories have none; narrow screens show none (the row text matters more).
/// </summary>
public static class PickerRowButtons
{
    /// <summary>Screens narrower than this show no buttons.</summary>
    public const int MinimumWidth = 60;

    /// <summary>The buttons of <paramref name="row"/> on a line of <paramref name="width"/> columns, left to right, with a space after the last.</summary>
    public static IReadOnlyList<TitleButton> Layout(PickerRow row, int width)
    {
        if (width < MinimumWidth || row.Tags.HasFlag(PickerTags.Directory))
        {
            return [];
        }

        var labels = new List<(string View, string Label)> { (DefaultMouseMap.PickerPinButton, row.IsPinned ? "[unpin]" : "[pin]") };
        if (row.IsRecent)
        {
            labels.Add((DefaultMouseMap.PickerRemoveButton, "[remove]"));
        }

        var total = labels.Sum(label => TextWidth.Of(label.Label)) + labels.Count;
        var buttons = new List<TitleButton>(labels.Count);
        var column = width - total;
        foreach (var (view, label) in labels)
        {
            buttons.Add(new TitleButton(view, label, column));
            column += TextWidth.Of(label) + 1;
        }

        return buttons;
    }
}
