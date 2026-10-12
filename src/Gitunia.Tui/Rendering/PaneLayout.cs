using Gitunia.Core.Workspaces;

namespace Gitunia.Tui.Rendering;

/// <summary>
/// Lays out the repo screen. Without a diff the detail pane spans the bottom (see <see cref="DetailLayout"/>). An open diff
/// sits to the right of a compact detail pane (the changed-files list, about a third of the width) and replaces it only on
/// very narrow screens. A zoomed pane takes the whole area
/// between the tab row and the status bar and hides the others.
/// </summary>
public static class PaneLayout
{
    /// <summary>Screens at least this wide show the detail pane and the diff side by side (at least 24 columns of file list and 45 of diff).</summary>
    public const int MinimumWidthForSideBySide = 70;

    /// <summary>Narrowest sidebar the user can drag to.</summary>
    public const int MinimumSidebarWidth = 12;

    /// <summary>Columns that stay for the graph when the sidebar is widened.</summary>
    public const int MinimumGraphWidth = 20;

    /// <summary>Fewest rows of the bottom strip (title row plus two rows).</summary>
    public const int MinimumBottomHeight = 3;

    /// <summary>Rows that stay for the graph when the bottom strip is made taller.</summary>
    public const int MinimumGraphHeight = 4;

    /// <summary>Narrowest detail pane the user can drag to beside a diff.</summary>
    public const int MinimumResizedDetailWidth = 12;

    /// <summary>Columns that stay for the diff when the detail pane is widened.</summary>
    public const int MinimumDiffWidth = 20;

    private const int MinimumDetailWidth = 24;
    private const int MaximumDetailWidth = 60;

    /// <summary>Columns of the detail pane next to an open diff, 0 when the diff replaces it.</summary>
    public static int DetailWidth(int screenWidth, bool isDiffOpen)
    {
        if (!isDiffOpen)
        {
            return screenWidth;
        }

        return screenWidth < MinimumWidthForSideBySide ? 0 : Math.Clamp(screenWidth / 3, MinimumDetailWidth, MaximumDetailWidth);
    }

    /// <summary>Columns of the detail pane next to an open diff, honouring the user's size.</summary>
    public static int DetailWidth(int screenWidth, bool isDiffOpen, PaneSizes? sizes)
    {
        var automatic = DetailWidth(screenWidth, isDiffOpen);
        if (automatic == 0 || !isDiffOpen || sizes?.DetailWidth is not { } wanted)
        {
            return automatic;
        }

        var maximum = Math.Max(MinimumResizedDetailWidth, screenWidth - 1 - MinimumDiffWidth);
        return Math.Clamp(wanted, MinimumResizedDetailWidth, maximum);
    }

    /// <summary>Columns of the sidebar list (without its divider), honouring the user's size; 0 while the screen is too narrow for it.</summary>
    public static int SidebarWidth(int screenWidth, PaneSizes? sizes)
    {
        var automatic = ScreenLayout.SidebarWidth(screenWidth);
        if (automatic == 0 || sizes?.SidebarWidth is not { } wanted)
        {
            return automatic;
        }

        var maximum = Math.Max(MinimumSidebarWidth, screenWidth - 1 - MinimumGraphWidth);
        return Math.Clamp(wanted, MinimumSidebarWidth, maximum);
    }

    /// <summary>Rows of the bottom strip including its title row, honouring the user's size; 0 while the screen is too short for it.</summary>
    public static int BottomHeight(int screenHeight, PaneSizes? sizes)
    {
        var automatic = DetailLayout.PaneHeight(screenHeight);
        if (automatic == 0 || sizes?.BottomHeight is not { } wanted)
        {
            return automatic;
        }

        var maximum = Math.Max(MinimumBottomHeight, ScreenLayout.GraphHeight(screenHeight) - MinimumGraphHeight);
        return Math.Clamp(wanted, MinimumBottomHeight, maximum);
    }

    /// <summary>The diff shares the detail pane's strip, so screens too short for that strip cannot open it (zoom still shows it).</summary>
    public static bool CanOpenDiff(int screenHeight) => DetailLayout.PaneHeight(screenHeight) > 0;

    /// <param name="screenWidth">Terminal columns.</param>
    /// <param name="screenHeight">Terminal rows.</param>
    /// <param name="isDiffOpen">The diff pane is open.</param>
    /// <param name="zoomed">The zoomed pane, null for none.</param>
    /// <param name="sizes">The sizes the user chose, null for all automatic.</param>
    public static PaneRects Compute(int screenWidth, int screenHeight, bool isDiffOpen, PaneKind? zoomed, PaneSizes? sizes = null)
    {
        var top = ScreenLayout.GraphTop;
        var area = ScreenLayout.GraphHeight(screenHeight);
        if (zoomed is { } zoomedPane && PaneZoom.CanZoom(zoomedPane))
        {
            return Zoomed(zoomedPane, screenWidth, top, area);
        }

        var paneHeight = isDiffOpen && !CanOpenDiff(screenHeight) ? 0 : BottomHeight(screenHeight, sizes);
        var graphHeight = Math.Max(0, area - paneHeight);
        var sidebarWidth = SidebarWidth(screenWidth, sizes);
        var graphLeft = sidebarWidth > 0 ? sidebarWidth + 1 : 0;
        var listHeight = Math.Max(0, graphHeight - 1);
        var contentHeight = Math.Max(0, paneHeight - 1);
        var bottomTop = top + graphHeight;
        var showDiff = isDiffOpen && paneHeight > 0;
        var detailWidth = showDiff ? DetailWidth(screenWidth, true, sizes) : screenWidth;
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
