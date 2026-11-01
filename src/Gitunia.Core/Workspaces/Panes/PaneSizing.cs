namespace Gitunia.Core.Workspaces.Panes;

/// <summary>
/// The pane size rules that do not depend on drawing: how wide the sidebar and the detail pane and how tall the bottom
/// strip are for a screen and the sizes the user chose. The resize keys (<see cref="PaneResizeRules"/>) and the layout
/// in the terminal layer share them, so a key never asks for a size the layout would not give.
/// </summary>
public static class PaneSizing
{
    /// <summary>Columns taken by the frame's left and right edges.</summary>
    public const int FrameColumns = 2;

    /// <summary>The first row of the panes below the frame's top edge.</summary>
    public const int GraphTop = 2;

    /// <summary>Narrowest terminal that still shows the sidebar; below it the graph gets the whole width.</summary>
    public const int MinimumWidthWithSidebar = 60;

    /// <summary>Screens shorter than this have no room for a pane next to a useful graph.</summary>
    public const int MinimumScreenHeight = 14;

    /// <summary>Screens at least this wide show the detail pane and the diff side by side (at least 24 columns of file list and 45 of diff).</summary>
    public const int MinimumWidthForSideBySide = 72;

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
    private const int MinimumPaneHeight = 6;
    private const int MaximumPaneHeight = 16;

    /// <summary>Columns between the frame's left and right edges.</summary>
    public static int InnerWidth(int screenWidth) => Math.Max(0, screenWidth - FrameColumns);

    /// <summary>Rows for the graph area: below the top edge and above the bottom edge and the status bar.</summary>
    public static int AreaHeight(int screenHeight) => Math.Max(0, screenHeight - GraphTop - 2);

    /// <summary>Columns of the automatic sidebar (without its divider), 0 when it is hidden.</summary>
    public static int AutomaticSidebarWidth(int screenWidth) =>
        screenWidth < MinimumWidthWithSidebar ? 0 : Math.Clamp(screenWidth / 4, 20, 36);

    /// <summary>Rows of the automatic bottom strip including its title row; 0 when the screen is too short.</summary>
    public static int AutomaticBottomHeight(int screenHeight) =>
        screenHeight < MinimumScreenHeight ? 0 : Math.Clamp(screenHeight / 3, MinimumPaneHeight, MaximumPaneHeight);

    /// <summary>Columns of the automatic detail pane next to an open diff, the whole width without one, 0 when the diff replaces it.</summary>
    public static int AutomaticDetailWidth(int screenWidth, bool isDiffOpen)
    {
        if (!isDiffOpen)
        {
            return InnerWidth(screenWidth);
        }

        return screenWidth < MinimumWidthForSideBySide ? 0 : Math.Clamp(screenWidth / 3, MinimumDetailWidth, MaximumDetailWidth);
    }

    /// <summary>Columns of the detail pane next to an open diff, honouring the user's size.</summary>
    public static int DetailWidth(int screenWidth, bool isDiffOpen, PaneSizes? sizes)
    {
        var automatic = AutomaticDetailWidth(screenWidth, isDiffOpen);
        if (automatic == 0 || !isDiffOpen || sizes?.DetailWidth is not { } wanted)
        {
            return automatic;
        }

        var maximum = Math.Max(MinimumResizedDetailWidth, InnerWidth(screenWidth) - 1 - MinimumDiffWidth);

        return Math.Clamp(wanted, MinimumResizedDetailWidth, maximum);
    }

    /// <summary>Columns of the sidebar list (without its divider), honouring the user's size; 0 while the screen is too narrow for it.</summary>
    public static int SidebarWidth(int screenWidth, PaneSizes? sizes)
    {
        var automatic = AutomaticSidebarWidth(screenWidth);
        if (automatic == 0 || sizes?.SidebarWidth is not { } wanted)
        {
            return automatic;
        }

        var maximum = Math.Max(MinimumSidebarWidth, InnerWidth(screenWidth) - 1 - MinimumGraphWidth);

        return Math.Clamp(wanted, MinimumSidebarWidth, maximum);
    }

    /// <summary>Rows of the bottom strip including its title row, honouring the user's size; 0 while the screen is too short for it.</summary>
    public static int BottomHeight(int screenHeight, PaneSizes? sizes)
    {
        var automatic = AutomaticBottomHeight(screenHeight);
        if (automatic == 0 || sizes?.BottomHeight is not { } wanted)
        {
            return automatic;
        }

        var maximum = Math.Max(MinimumBottomHeight, AreaHeight(screenHeight) - MinimumGraphHeight);

        return Math.Clamp(wanted, MinimumBottomHeight, maximum);
    }
}
