using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces;

namespace Gitunia.Tui.Rendering;

/// <summary>
/// The tab labels at the left end of the detail pane's title row. The same layout draws them and finds the one under a click, so the
/// two never disagree. Narrow rows drop the padding around the labels.
/// </summary>
public static class DetailTabStrip
{
    private static readonly (BottomTab Tab, string Name, string View)[] Tabs =
    [
        (BottomTab.Details, "Details", DefaultMouseMap.DetailDetailsTab),
        (BottomTab.Changes, "Changes", DefaultMouseMap.DetailChangesTab),
        (BottomTab.Files, "Files", DefaultMouseMap.DetailFilesTab),
    ];

    /// <summary>The tab labels for a title row of <paramref name="width"/> columns, left to right.</summary>
    public static IReadOnlyList<TitleButton> Layout(int width)
    {
        var padded = Tabs.Sum(tab => tab.Name.Length + 2) <= width;
        var buttons = new List<TitleButton>(Tabs.Length);
        var column = padded ? 0 : 1;
        foreach (var tab in Tabs)
        {
            var label = padded ? " " + tab.Name + " " : tab.Name;
            buttons.Add(new TitleButton(tab.View, label, column));
            column += label.Length + (padded ? 0 : 1);
        }

        return buttons;
    }

    /// <summary>The tab a label of the strip stands for.</summary>
    public static BottomTab TabOf(TitleButton button) => Tabs.First(tab => tab.View == button.View).Tab;
}
