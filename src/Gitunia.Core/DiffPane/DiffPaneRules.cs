using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.DiffPane;

/// <summary>The layout rules of the diff pane that do not depend on drawing: where it is shown and how a zoom behaves.</summary>
public static class DiffPaneRules
{
    /// <summary>
    /// The Changes tab always shows the diff beside its file list (the Files tab the content of a file), where the
    /// screen has room for both. False while the terminal layer has not reported the screen size.
    /// </summary>
    /// <param name="screen">The screen size, null when it is not known.</param>
    /// <param name="bottomTab">The tab of the bottom detail pane that is showing.</param>
    public static bool ShowsWithChanges(ScreenExtent? screen, BottomTab bottomTab) =>
        screen is { } size && PaneSizing.ShowsDiffWithChanges(size.Width, size.Height, bottomTab);

    /// <summary>The Files tab is showing and the screen has room for its content beside the tree.</summary>
    /// <param name="screen">The screen size, null when it is not known.</param>
    /// <param name="bottomTab">The tab of the bottom detail pane that is showing.</param>
    public static bool FilesBeside(ScreenExtent? screen, BottomTab bottomTab) =>
        bottomTab == BottomTab.Files && ShowsWithChanges(screen, bottomTab);

    /// <summary>The diff pane is on screen: opened on demand, or shown by the Changes tab.</summary>
    /// <param name="diff">The pane state.</param>
    /// <param name="showsWithChanges">The screen shows the pane beside the file list.</param>
    public static bool IsShown(DiffPaneState diff, bool showsWithChanges) => diff.IsOpen || showsWithChanges;

    /// <summary>Changes (or Files) and the diff beside it zoom as one view: focus moving between them keeps the zoom (#632).</summary>
    /// <param name="zoomed">The zoomed pane.</param>
    /// <param name="focused">The focused pane.</param>
    /// <param name="showsWithChanges">The screen shows the pane beside the file list.</param>
    public static bool KeepsZoomTogether(PaneKind? zoomed, PaneKind? focused, bool showsWithChanges) =>
        zoomed is { } zoom && focused is { } focus && PaneZoom.IsZoomPair(zoom, focus) && showsWithChanges;

    /// <summary>The pane that is zoomed now: a zoom that does not match the focused pane is not part of the layout.</summary>
    /// <param name="diff">The pane state.</param>
    /// <param name="focused">The focused pane.</param>
    /// <param name="showsWithChanges">The screen shows the pane beside the file list.</param>
    public static PaneKind? EffectiveZoom(DiffPaneState diff, PaneKind? focused, bool showsWithChanges)
    {
        var together = KeepsZoomTogether(diff.Zoomed, focused, showsWithChanges);

        return together || diff.Zoomed == focused ? focused : null;
    }

    /// <summary>Opening a file keeps a zoom of the pair; on a screen too short for the strip the diff opens zoomed.</summary>
    /// <param name="diff">The pane state.</param>
    /// <param name="screen">The screen size, null when it is not known.</param>
    /// <param name="showsWithChanges">The screen shows the pane beside the file list.</param>
    public static PaneKind? ZoomAfterOpening(DiffPaneState diff, ScreenExtent? screen, bool showsWithChanges)
    {
        if (screen is { } size && !PaneSizing.CanOpenDiff(size.Height))
        {
            return PaneKind.Diff;
        }

        return KeepsZoomTogether(diff.Zoomed, PaneKind.Diff, showsWithChanges) ? diff.Zoomed : null;
    }
}
