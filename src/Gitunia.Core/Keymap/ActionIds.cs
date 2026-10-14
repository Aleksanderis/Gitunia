using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Core.Keymap;

/// <summary>Identifiers of the actions the default keymap binds. The state layer maps them to commands.</summary>
public static class ActionIds
{
    public static ActionId Cancel { get; } = new("cancel");

    public static ActionId CommandExecute { get; } = new("command.execute");

    public static ActionId ConfirmNo { get; } = new("confirm.no");

    public static ActionId ConfirmYes { get; } = new("confirm.yes");

    public static ActionId DetailCopyPath { get; } = new("detail.copy-path");

    public static ActionId DetailNextTab { get; } = new("detail.next-tab");

    public static ActionId DetailOpenDiff { get; } = new("detail.open-diff");

    public static ActionId DetailPreviousTab { get; } = new("detail.previous-tab");

    public static ActionId DetailRetry { get; } = new("detail.retry");

    public static ActionId DetailTabChanges { get; } = new("detail.tab-changes");

    public static ActionId DetailTabDetails { get; } = new("detail.tab-details");

    public static ActionId DetailTabFiles { get; } = new("detail.tab-files");

    public static ActionId DiffCopyFile { get; } = new("diff.copy-file");

    public static ActionId DiffCopyLine { get; } = new("diff.copy-line");

    public static ActionId DiffLoadAnyway { get; } = new("diff.load-anyway");

    public static ActionId DiffNextFile { get; } = new("diff.next-file");

    public static ActionId DiffNextHunk { get; } = new("diff.next-hunk");

    public static ActionId DiffPreviousFile { get; } = new("diff.previous-file");

    public static ActionId DiffPreviousHunk { get; } = new("diff.previous-hunk");

    public static ActionId DiffSearchNext { get; } = new("diff.search-next");

    public static ActionId DiffSearchOpen { get; } = new("diff.search-open");

    public static ActionId DiffSearchPrevious { get; } = new("diff.search-previous");

    public static ActionId DiffToggleWrap { get; } = new("diff.toggle-wrap");

    public static ActionId GraphCopyHash { get; } = new("graph.copy-hash");

    public static ActionId GraphJumpToHead { get; } = new("graph.jump-to-head");

    public static ActionId GraphJumpToRef { get; } = new("graph.jump-to-ref");

    public static ActionId GraphOpenDetails { get; } = new("graph.open-details");

    public static ActionId HelpShow { get; } = new("help.show");

    public static ActionId InputAccept { get; } = new("input.accept");

    public static ActionId ListBottom { get; } = new("list.bottom");

    public static ActionId ListDown { get; } = new("list.down");

    public static ActionId ListHalfPageDown { get; } = new("list.half-page-down");

    public static ActionId ListHalfPageUp { get; } = new("list.half-page-up");

    public static ActionId ListTop { get; } = new("list.top");

    public static ActionId ListUp { get; } = new("list.up");

    public static ActionId NavigateBack { get; } = new("navigate.back");

    public static ActionId PaletteNext { get; } = new("palette.next");

    public static ActionId PaletteOpen { get; } = new("palette.open");

    public static ActionId PalettePrevious { get; } = new("palette.previous");

    public static ActionId PaneFocusDown { get; } = new("pane.focus-down");

    public static ActionId PaneFocusLeft { get; } = new("pane.focus-left");

    public static ActionId PaneFocusRight { get; } = new("pane.focus-right");

    public static ActionId PaneFocusUp { get; } = new("pane.focus-up");

    public static ActionId PaneNext { get; } = new("pane.next");

    public static ActionId PanePrevious { get; } = new("pane.previous");

    public static ActionId PaneZoom { get; } = new("pane.zoom");

    public static ActionId PaneWider { get; } = new("pane.wider");

    public static ActionId PaneNarrower { get; } = new("pane.narrower");

    public static ActionId PaneTaller { get; } = new("pane.taller");

    public static ActionId PaneShorter { get; } = new("pane.shorter");

    public static ActionId PaneResetSizes { get; } = new("pane.reset-sizes");

    public static ActionId PickerAccept { get; } = new("picker.accept");

    public static ActionId PickerCompletePath { get; } = new("picker.complete-path");

    public static ActionId PickerNext { get; } = new("picker.next");

    public static ActionId PickerOpen { get; } = new("picker.open");

    public static ActionId PickerPin { get; } = new("picker.pin");

    public static ActionId PickerPrevious { get; } = new("picker.previous");

    public static ActionId PickerRemoveRecent { get; } = new("picker.remove-recent");

    public static ActionId RepoPin { get; } = new("repo.pin");

    public static ActionId RepoRefresh { get; } = new("repo.refresh");

    public static ActionId SearchNext { get; } = new("search.next");

    public static ActionId SearchOpen { get; } = new("search.open");

    public static ActionId SearchPrevious { get; } = new("search.previous");

    public static ActionId SidebarActivate { get; } = new("sidebar.activate");

    public static ActionId SidebarCollapse { get; } = new("sidebar.collapse");

    public static ActionId SidebarCopy { get; } = new("sidebar.copy");

    public static ActionId SidebarExpand { get; } = new("sidebar.expand");

    public static ActionId SidebarFilter { get; } = new("sidebar.filter");

    public static ActionId StatusDismiss { get; } = new("status.dismiss");

    public static ActionId StatusHistory { get; } = new("status.history");

    public static ActionId TabClose { get; } = new("tab.close");

    public static ActionId TabLastActive { get; } = new("tab.last-active");

    public static ActionId TabNext { get; } = new("tab.next");

    public static ActionId TabPrevious { get; } = new("tab.previous");

    public static ActionId TabRename { get; } = new("tab.rename");

    public static ActionId VisualCopy { get; } = new("visual.copy");

    public static ActionId VisualToggle { get; } = new("visual.toggle");

    /// <summary>Jump to tab <paramref name="number"/> (1-based).</summary>
    public static ActionId TabGoTo(int number) => new($"tab.go-to.{number}");
}
