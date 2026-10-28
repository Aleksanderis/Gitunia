using System.Globalization;

namespace Gitunia.Core.Overlays;

/// <summary>The rows of a candidate list in a prompt popup: a scrolling window, the highlighted row marked.</summary>
public static class PickerRows
{
    /// <summary>How many candidate rows are shown at most; the list scrolls to keep the highlight in view.</summary>
    public const int VisibleRows = 8;

    /// <summary>The window of <paramref name="items"/> around <paramref name="selected"/>, which is marked.</summary>
    public static List<string> Window(IReadOnlyList<string> items, int selected)
    {
        var first = Math.Clamp(selected - (VisibleRows / 2), 0, Math.Max(0, items.Count - VisibleRows));
        return
        [
            .. items.Skip(first).Take(VisibleRows)
                .Select((item, offset) => $"{(first + offset == selected ? "> " : "  ")}{item}"),
        ];
    }

    /// <summary>The <c>(n/m)</c> text for a 0-based index.</summary>
    public static string Position(int index, int total) =>
        $"({(index + 1).ToString(CultureInfo.InvariantCulture)}/{total.ToString(CultureInfo.InvariantCulture)})";
}
