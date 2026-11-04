using Gitunia.Core.Picker;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>
/// Where the parts of the picker sit inside its area, shared by the renderer and the mouse. Lines are relative to the
/// box (<see cref="PickerComposer.Compose"/> returns them), the box sits at (<see cref="BoxX"/>, <see cref="BoxY"/>) of the
/// area. A tiny area gets no frame: query line, list and notice only.
/// </summary>
public sealed record PickerLayout(
    bool IsFramed,
    int BoxX,
    int BoxY,
    int BoxWidth,
    int BoxHeight,
    int ListFirstLine,
    int ListHeight,
    int MessageLine,
    IReadOnlyList<PickerListEntry> Visible)
{
    private const int MaximumWidth = 100;
    private const int MaximumListRows = PickerListPlan.MaximumListRows;
    private const int MinimumListRows = PickerListPlan.MinimumListRows;
    private const int FramedChromeRows = PickerListPlan.FramedChromeRows;
    private const int MinimumFramedWidth = 30;
    private const int Margin = 2;

    /// <summary>Columns between the box edges and the content.</summary>
    public int InnerWidth => IsFramed ? BoxWidth - 2 : BoxWidth;

    /// <summary>Whether box line <paramref name="line"/> is the footer row of chips (the framed box only).</summary>
    public bool IsFooterLine(int line) => IsFramed && line == BoxHeight - 2;

    /// <summary>The repository row index drawn on box line <paramref name="line"/>, or null (a header, a spacer or a more or less line).</summary>
    public int? RowIndexAt(int line) => EntryAt(line) is { Kind: PickerEntryKind.Row } entry ? entry.RowIndex : null;

    /// <summary>The selectable list line (row, more or less) drawn on box line <paramref name="line"/>, or null.</summary>
    public PickerListEntry? EntryAt(int line)
    {
        var offset = line - ListFirstLine;
        return offset >= 0 && offset < Visible.Count && Visible[offset].IsSelectable ? Visible[offset] : null;
    }

    /// <summary>
    /// The lines of the list for an area of <paramref name="areaHeight"/> rows, as <see cref="PickerListPlan"/> lays them
    /// out; the blank spacers above the headers are left out unless <paramref name="withSpacers"/> (the framed box; the
    /// unframed one is too small).
    /// </summary>
    public static IReadOnlyList<PickerListEntry> ListLines(PickerState picker, int areaHeight, bool withSpacers)
    {
        var plan = PickerListPlan.Build(picker, PickerListPlan.Budget(areaHeight));

        return withSpacers ? plan : [.. plan.Where(entry => entry.Kind != PickerEntryKind.Spacer)];
    }

    public static PickerLayout Compute(PickerState picker, int width, int height)
    {
        var hasMessage = picker.Message.Length > 0;
        var chrome = FramedChromeRows + (hasMessage ? 1 : 0);
        if (width >= MinimumFramedWidth && height >= chrome + 1)
        {
            var all = ListLines(picker, height, true);
            var boxWidth = Math.Min(MaximumWidth, width - Margin);
            var listHeight = Math.Min(Math.Min(MaximumListRows, height - chrome), Math.Max(MinimumListRows, all.Count));
            var boxHeight = chrome + listHeight;
            var messageLine = hasMessage ? 3 + listHeight : -1;
            return new PickerLayout(
                true,
                (width - boxWidth) / 2,
                (height - boxHeight) / 3,
                boxWidth,
                boxHeight,
                3,
                listHeight,
                messageLine,
                Window(all, picker, listHeight));
        }

        var compact = ListLines(picker, height, false);
        var compactList = Math.Max(0, height - 1 - (hasMessage ? 1 : 0));
        return new PickerLayout(
            false,
            0,
            0,
            Math.Max(0, width),
            Math.Max(0, height),
            1,
            compactList,
            hasMessage && height > 1 ? 1 + compactList : -1,
            Window(compact, picker, compactList));
    }

    /// <summary>The first list row shown for a highlight, so the highlight stays visible.</summary>
    public static int TopRow(int highlight, int listHeight) => highlight < listHeight ? 0 : highlight - listHeight + 1;

    private static List<PickerListEntry> Window(IReadOnlyList<PickerListEntry> all, PickerState picker, int listHeight)
    {
        if (listHeight <= 0)
        {
            return [];
        }

        var at = PickerListPlan.PositionOf(all, picker);
        var top = TopRow(Math.Max(0, at), listHeight);
        if (top < at && all[top].Kind == PickerEntryKind.Spacer)
        {
            top++;
        }

        return [.. all.Skip(top).Take(listHeight)];
    }
}
