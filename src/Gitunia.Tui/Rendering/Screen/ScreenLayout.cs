using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>
/// Where things sit on the screen: the tab row, then the frame (a top edge row, the panes, a bottom edge row) and the status bar.
/// Shared by the views and the text frame. Every pane is a box (see <see cref="FrameGrid"/>): the first and last column and
/// the edge rows belong to the frame, and a pane's text is padded by <see cref="PanePadding.Columns"/> columns inside its box.
/// </summary>
public static class ScreenLayout
{
    public const int TabRow = 0;

    /// <summary>The row of the frame's top edge; the sidebar and graph titles sit on it.</summary>
    public const int FrameTop = 1;

    /// <summary>The first row of the panes below the frame's top edge.</summary>
    public const int GraphTop = 2;

    /// <summary>Columns taken by the frame's left and right edges.</summary>
    public const int FrameColumns = 2;

    /// <summary>Rows of the graph list that precede the first commit: the slot reserved for the working-tree pseudo-row (none yet).</summary>
    public const int ReservedLeadingRows = 0;

    /// <summary>Narrowest terminal that still shows the sidebar; below it the graph gets the whole width.</summary>
    public const int MinimumWidthWithSidebar = 60;

    /// <summary>Columns of the sidebar (between the left edge and the divider) on a screen of <paramref name="screenWidth"/> columns, 0 when it is hidden.</summary>
    public static int SidebarWidth(int screenWidth) => screenWidth < MinimumWidthWithSidebar ? 0 : Math.Clamp(screenWidth / 4, 20, 36);

    /// <summary>First column of the graph: after the left edge, the sidebar and its divider.</summary>
    public static int GraphLeft(int screenWidth) => SidebarWidth(screenWidth) is var sidebar and > 0 ? sidebar + 2 : 1;

    /// <summary>Columns between the frame's left and right edges.</summary>
    public static int InnerWidth(int screenWidth) => Math.Max(0, screenWidth - FrameColumns);

    /// <summary>Rows for the graph in a screen of <paramref name="screenHeight"/> rows: below the top edge and above the bottom edge and the status bar.</summary>
    public static int GraphHeight(int screenHeight) => Math.Max(0, screenHeight - GraphTop - 2);

    /// <summary>The row of the frame's bottom edge.</summary>
    public static int FrameBottom(int screenHeight) => Math.Max(0, screenHeight - 2);

    /// <summary>The status bar is the last row.</summary>
    public static int StatusRow(int screenHeight) => Math.Max(0, screenHeight - 1);
}
