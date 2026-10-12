namespace Gitunia.Tui.Rendering;

/// <summary>Where the detail pane sits: along the bottom, between the graph and the status bar.</summary>
public static class DetailLayout
{
    /// <summary>Screens shorter than this have no room for a pane next to a useful graph.</summary>
    public const int MinimumScreenHeight = 14;

    private const int MinimumPaneHeight = 6;
    private const int MaximumPaneHeight = 16;

    /// <summary>Rows of the pane including its title row; 0 when the screen is too short.</summary>
    public static int PaneHeight(int screenHeight) =>
        screenHeight < MinimumScreenHeight ? 0 : Math.Clamp(screenHeight / 3, MinimumPaneHeight, MaximumPaneHeight);

    /// <summary>Rows left for the graph once the pane is shown.</summary>
    public static int GraphHeight(int screenHeight) => Math.Max(0, ScreenLayout.GraphHeight(screenHeight) - PaneHeight(screenHeight));

    /// <summary>Rows of the pane below its title.</summary>
    public static int ContentHeight(int screenHeight) => Math.Max(0, PaneHeight(screenHeight) - 1);
}
