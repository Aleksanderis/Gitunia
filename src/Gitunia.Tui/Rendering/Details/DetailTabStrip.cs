using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>
/// The tab labels at the left end of the detail pane's title row. The same layout draws them and finds the one under a click, so the
/// two never disagree. The row is the top edge of the pane's box, so the labels start after one stretch of line.
/// Each label is a button: padded with a space on each side, one column of line between them; narrow rows drop the padding.
/// </summary>
public static class DetailTabStrip
{
    private static readonly DetailTabSpecification[] Tabs =
    [
        new(BottomTab.Details, "Details", DefaultMouseMap.DetailDetailsTab),
        new(BottomTab.Changes, "Changes", DefaultMouseMap.DetailChangesTab),
        new(BottomTab.Files, "Files", DefaultMouseMap.DetailFilesTab),
    ];

    /// <summary>The tab labels for a title row of <paramref name="width"/> columns, left to right.</summary>
    public static IReadOnlyList<TitleButton> Layout(int width)
    {
        var padded = Tabs.Sum(tab => tab.Name.Length + 3) + 1 <= width;
        var buttons = new List<TitleButton>(Tabs.Length);
        var column = 1;
        foreach (var tab in Tabs)
        {
            var label = padded ? $" {tab.Name} " : tab.Name;
            buttons.Add(new TitleButton(tab.View, label, column));
            column += label.Length + 1;
        }

        return buttons;
    }

    /// <summary>The tab a label of the strip stands for.</summary>
    public static BottomTab TabOf(TitleButton button) => Tabs.First(tab => tab.View == button.View).Tab;
}
