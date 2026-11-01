using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Workspaces.Panes;

/// <summary>The pane resize keys as a pure transition: the new sizes go into the tab's view state, a refusal is a notice.</summary>
public static class PaneSizeReducer
{
    /// <summary>The notice for a resize while a pane is zoomed.</summary>
    public const string ZoomedNotice = "Unzoom the pane to resize";

    /// <summary>The notice for the reset key when the sizes are automatic already.</summary>
    public const string AlreadyAutomaticNotice = "Pane sizes are already automatic";

    /// <summary>The notice for a resize that is at its limit.</summary>
    public const string AtLimitNotice = "This pane cannot be resized further";

    /// <summary>Applies the key; a tab that is not open any more, or no known screen size, changes nothing.</summary>
    public static AppUpdate Reduce(AppState state, PaneSizeCommand command)
    {
        if (state.Workspace.FindTab(command.TabId) is not { } tab || state.Shell.Screen is not { } screen)
        {
            return new AppUpdate(state, []);
        }

        var layout = command.Layout;
        var heightKey = command.Action == ActionIds.PaneTaller || command.Action == ActionIds.PaneShorter;
        var noDivider = layout.Zoomed is not null && (!layout.ZoomBoth || heightKey);
        if (noDivider || layout.IsWorkingDiffZoomed)
        {
            return Notice(state, ZoomedNotice);
        }

        var view = tab.View;
        var current = new PaneSizes(view.SidebarWidth, view.BottomHeight, view.DetailWidth);
        var sizes = PaneResizeRules.Apply(
            current,
            command.Action,
            command.Count,
            view.FocusedPane,
            screen.Width,
            screen.Height,
            layout.IsDiffOpen);
        if (sizes is null)
        {
            return Notice(state, command.Action == ActionIds.PaneResetSizes ? AlreadyAutomaticNotice : AtLimitNotice);
        }

        var update = WorkspaceReducer.Reduce(
            state.Workspace,
            new TabCommand(tab.Id, new SetPaneSizesCommand(sizes.SidebarWidth, sizes.BottomHeight, sizes.DetailWidth)));

        return new AppUpdate(state with { Workspace = update.Workspace }, [.. update.Effects]);
    }

    private static AppUpdate Notice(AppState state, string text) =>
        new(state, [new PostStatusEffect(text, StatusSeverity.Info)]);
}
