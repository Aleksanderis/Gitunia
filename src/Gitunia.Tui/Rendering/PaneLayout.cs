using Gitunia.Core.Workspaces;

namespace Gitunia.Tui.Rendering;

/// <summary>
/// Lays out the repo screen. Without a diff the detail pane spans the bottom (see <see cref="DetailLayout"/>). An open diff
/// sits to the right of the detail pane on wide screens and replaces it on narrow ones. A zoomed pane takes the whole
/// area between the tab row and the status bar and hides the others.
/// </summary>
public static class PaneLayout
{
    /// <summary>Screens at least this wide show the detail pane and the diff side by side.</summary>
    public const int MinimumWidthForSideBySide = 100;

    private const int MinimumDetailWidth = 36;
    private const int MaximumDetailWidth = 60;

    /// <summary>Columns of the detail pane next to an open diff, 0 when the diff replaces it.</summary>
    public static int DetailWidth(int screenWidth, bool isDiffOpen)
    {
        if (!isDiffOpen)
        {
            return screenWidth;
        }

        return screenWidth < MinimumWidthForSideBySide ? 0 : Math.Clamp(screenWidth * 2 / 5, MinimumDetailWidth, MaximumDetailWidth);
    }

    /// <summary>The diff shares the detail pane's strip, so screens too short for that strip cannot open it (zoom still shows it).</summary>
    public static bool CanOpenDiff(int screenHeight) => DetailLayout.PaneHeight(screenHeight) > 0;

    /// <param name="screenWidth">Terminal columns.</param>
    /// <param name="screenHeight">Terminal rows.</param>
    /// <param name="isDiffOpen">The diff pane is open.</param>
    /// <param name="zoomed">The zoomed pane, null for none.</param>
    public static PaneRects Compute(int screenWidth, int screenHeight, bool isDiffOpen, PaneKind? zoomed)
    {
        var top = ScreenLayout.GraphTop;
        var area = ScreenLayout.GraphHeight(screenHeight);
        if (zoomed is { } zoomedPane && PaneZoom.CanZoom(zoomedPane))
        {
            return Zoomed(zoomedPane, screenWidth, top, area);
        }

        var paneHeight = isDiffOpen && !CanOpenDiff(screenHeight) ? 0 : DetailLayout.PaneHeight(screenHeight);
        var graphHeight = Math.Max(0, area - paneHeight);
        var sidebarWidth = ScreenLayout.SidebarWidth(screenWidth);
        var graphLeft = ScreenLayout.GraphLeft(screenWidth);
        var listHeight = Math.Max(0, graphHeight - 1);
        var contentHeight = Math.Max(0, paneHeight - 1);
        var bottomTop = top + graphHeight;
        var showDiff = isDiffOpen && paneHeight > 0;
        var detailWidth = showDiff ? DetailWidth(screenWidth, true) : screenWidth;
        var diffLeft = detailWidth > 0 ? detailWidth + 1 : 0;
        var diffWidth = showDiff ? Math.Max(0, screenWidth - diffLeft) : 0;
        var none = new PaneRect(0, 0, 0, 0);
        return new PaneRects(
            new PaneRect(0, top, sidebarWidth, listHeight),
            new PaneRect(0, top + listHeight, sidebarWidth, sidebarWidth > 0 && graphHeight > 0 ? 1 : 0),
            new PaneRect(sidebarWidth, top, sidebarWidth > 0 ? 1 : 0, graphHeight),
            new PaneRect(graphLeft, top, screenWidth - graphLeft, graphHeight),
            paneHeight > 0 && detailWidth > 0 ? new PaneRect(0, bottomTop, detailWidth, 1) : none,
            paneHeight > 0 && detailWidth > 0 ? new PaneRect(0, bottomTop + 1, detailWidth, contentHeight) : none,
            showDiff && detailWidth > 0 ? new PaneRect(detailWidth, bottomTop, 1, paneHeight) : none,
            showDiff ? new PaneRect(diffLeft, bottomTop, diffWidth, 1) : none,
            showDiff ? new PaneRect(diffLeft, bottomTop + 1, diffWidth, contentHeight) : none);
    }

    private static PaneRects Zoomed(PaneKind pane, int width, int top, int area)
    {
        var none = new PaneRect(0, 0, 0, 0);
        var title = new PaneRect(0, top, width, area > 0 ? 1 : 0);
        var content = new PaneRect(0, top + 1, width, Math.Max(0, area - 1));
        return new PaneRects(
            none,
            none,
            none,
            pane == PaneKind.Graph ? new PaneRect(0, top, width, area) : none,
            pane == PaneKind.Detail ? title : none,
            pane == PaneKind.Detail ? content : none,
            none,
            pane == PaneKind.Diff ? title : none,
            pane == PaneKind.Diff ? content : none);
    }
}
