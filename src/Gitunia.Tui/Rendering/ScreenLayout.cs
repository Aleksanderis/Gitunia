namespace Gitunia.Tui.Rendering;

/// <summary>Where things sit on the screen: header, tab row, graph, status bar. Shared by the views and the text frame.</summary>
public static class ScreenLayout
{
    public const int HeaderRow = 0;
    public const int TabRow = 1;
    public const int GraphTop = 2;

    /// <summary>Rows of the graph list that precede the first commit: the slot reserved for the working-tree pseudo-row (none yet).</summary>
    public const int ReservedLeadingRows = 0;

    /// <summary>Narrowest terminal that still shows the sidebar; below it the graph gets the whole width.</summary>
    public const int MinimumWidthWithSidebar = 60;

    /// <summary>Columns of the sidebar (without the divider) on a screen of <paramref name="screenWidth"/> columns, 0 when it is hidden.</summary>
    public static int SidebarWidth(int screenWidth) => screenWidth < MinimumWidthWithSidebar ? 0 : Math.Clamp(screenWidth / 4, 20, 36);

    /// <summary>First column of the graph: after the sidebar and its divider.</summary>
    public static int GraphLeft(int screenWidth) => SidebarWidth(screenWidth) is var sidebar and > 0 ? sidebar + 1 : 0;

    /// <summary>Rows for the graph in a screen of <paramref name="screenHeight"/> rows.</summary>
    public static int GraphHeight(int screenHeight) => Math.Max(0, screenHeight - GraphTop - 1);

    /// <summary>The status bar is the last row.</summary>
    public static int StatusRow(int screenHeight) => Math.Max(0, screenHeight - 1);
}
