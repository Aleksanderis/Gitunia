using System.Globalization;

namespace Gitunia.Core.Overlays;

/// <summary>
/// The rows of a candidate list in a prompt popup: a scrolling window and the index of its highlighted row, which the
/// popup draws as the selection bar (like the repository picker and the palette), not as a text mark.
/// </summary>
public static class PickerRows
{
    /// <summary>How many candidate rows are shown at most; the list scrolls to keep the highlight in view.</summary>
    public const int VisibleRows = 8;

    /// <summary>
    /// All <paramref name="items"/> with <paramref name="selected"/> highlighted. The popup builder windows them once, to
    /// <see cref="VisibleRows"/> or what the screen leaves (<c>ListViewport</c>), so the list is not windowed twice.
    /// </summary>
    public static PickerWindow Window(IReadOnlyList<string> items, int selected) =>
        new(items, selected, Position(selected, items.Count));

    /// <summary>The <c>n/m</c> text for a 0-based index.</summary>
    public static string Position(int index, int total) =>
        $"{(index + 1).ToString(CultureInfo.InvariantCulture)}/{total.ToString(CultureInfo.InvariantCulture)}";
}
