using Gitunia.Core.Picker;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>One line of the picker list: a group header (<see cref="RowIndex"/> is -1) or a repository row.</summary>
public sealed record PickerListLine(int RowIndex, string Header);

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
    int QueryLine,
    int ListFirstLine,
    int ListHeight,
    int MessageLine,
    int FooterLine,
    IReadOnlyList<PickerListLine> Visible)
{
    private const int MaximumWidth = 100;
    private const int MaximumListRows = 14;
    private const int MinimumListRows = 4;
    private const int FramedChromeRows = 5;
    private const int MinimumFramedWidth = 30;
    private const int Margin = 2;

    /// <summary>Columns between the box edges and the content.</summary>
    public int InnerWidth => IsFramed ? BoxWidth - 2 : BoxWidth;

    /// <summary>The repository row index drawn on box line <paramref name="line"/>, or null.</summary>
    public int? RowIndexAt(int line)
    {
        var offset = line - ListFirstLine;
        return offset >= 0 && offset < Visible.Count && Visible[offset].RowIndex >= 0 ? Visible[offset].RowIndex : null;
    }

    /// <summary>The group headers and rows of the list: headers only for the unfiltered list with more than one group.</summary>
    public static IReadOnlyList<PickerListLine> ListLines(PickerState picker)
    {
        var lines = new List<PickerListLine>(picker.Rows.Length + 4);
        var withHeaders = picker.Query.Length == 0
            && !picker.IsPathMode
            && picker.Rows.Select(row => Group(row.Tags)).Distinct().Count() > 1;
        var previous = -1;
        for (var index = 0; index < picker.Rows.Length; index++)
        {
            var group = Group(picker.Rows[index].Tags);
            if (withHeaders && group != previous)
            {
                lines.Add(new PickerListLine(-1, GroupName(group)));
            }

            previous = group;
            lines.Add(new PickerListLine(index, string.Empty));
        }

        return lines;
    }

    public static PickerLayout Compute(PickerState picker, int width, int height)
    {
        var all = ListLines(picker);
        var hasMessage = picker.Message.Length > 0;
        var chrome = FramedChromeRows + (hasMessage ? 1 : 0);
        if (width >= MinimumFramedWidth && height >= chrome + 1)
        {
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
                1,
                3,
                listHeight,
                messageLine,
                boxHeight - 2,
                Window(all, picker.Highlight, listHeight));
        }

        var compactList = Math.Max(0, height - 1 - (hasMessage ? 1 : 0));
        return new PickerLayout(
            false,
            0,
            0,
            Math.Max(0, width),
            Math.Max(0, height),
            0,
            1,
            compactList,
            hasMessage && height > 1 ? 1 + compactList : -1,
            -1,
            Window(all, picker.Highlight, compactList));
    }

    /// <summary>The first list row shown for a highlight, so the highlight stays visible.</summary>
    public static int TopRow(int highlight, int listHeight) => highlight < listHeight ? 0 : highlight - listHeight + 1;

    private static List<PickerListLine> Window(IReadOnlyList<PickerListLine> all, int highlight, int listHeight)
    {
        if (listHeight <= 0)
        {
            return [];
        }

        var at = all.ToList().FindIndex(line => line.RowIndex == highlight);
        var top = TopRow(Math.Max(0, at), listHeight);
        return [.. all.Skip(top).Take(listHeight)];
    }

    // Open, pinned, recent, then the rest (found, directories).
    private static int Group(PickerTags tags)
    {
        if (tags.HasFlag(PickerTags.Open))
        {
            return 0;
        }

        if (tags.HasFlag(PickerTags.Pinned))
        {
            return 1;
        }

        return tags.HasFlag(PickerTags.Recent) ? 2 : 3;
    }

    private static string GroupName(int group) => group switch
    {
        0 => "Open",
        1 => "Pinned",
        2 => "Recent",
        _ => "Found",
    };
}
