using Gitunia.Core.Conflicts;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Operations;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Tabs;
using Gitunia.Tui.Rendering.WorkingTree;

namespace Gitunia.Tui.App;

/// <summary>Derives what the chrome shows from the workspace state and the tab's runtime session.</summary>
public static class ScreenModelFactory
{
    /// <param name="workspace">The workspace; its active tab is shown.</param>
    /// <param name="session">The active tab's session, null while it is not created yet.</param>
    /// <param name="mode">Input mode.</param>
    /// <param name="pendingKeys">The unfinished chord or count.</param>
    /// <param name="message">A short notice, empty when none.</param>
    /// <param name="selectedItem">The selected list item, null when nothing is selected.</param>
    /// <param name="tabs">The tab row labels, null for the single-label fallback.</param>
    /// <param name="picker">The open repository picker, null when closed.</param>
    /// <param name="homeDirectory">The home directory the picker abbreviates to <c>~</c>.</param>
    /// <param name="keymap">The running keymap the banner names its keys from; null names the defaults.</param>
    public static ScreenModel Create(
        Workspace workspace,
        TerminalTabSession? session,
        KeyMode mode,
        string pendingKeys,
        string message,
        int? selectedItem,
        IReadOnlyList<TabLabel>? tabs = null,
        PickerState? picker = null,
        string homeDirectory = "",
        KeyBindingTable? keymap = null)
    {
        var tab = workspace.ActiveTab;
        var tabIndex = tab is null ? 0 : workspace.Tabs.IndexOf(tab);
        var loaded = session?.Graph.Count ?? 0;
        var position = selectedItem is { } item && loaded > 0 ? Math.Clamp(item - ScreenLayout.ReservedLeadingRows, 0, loaded - 1) + 1 : 0;
        if (tab is { View.Mode: TabViewMode.WorkingTree })
        {
            // The Changes view counts the files of the list the cursor is in.
            var place = WorkingTreePosition.From(tab.WorkingTree);
            position = place.Selected;
            loaded = place.Count;
        }

        var missingNotice = picker is null ? MissingNotice(tab) : string.Empty;
        var notice = session?.LoadError is { Length: > 0 } loadError ? loadError : missingNotice;

        var screenModel = new ScreenModel(
            tab?.DisplayName ?? string.Empty,
            tab?.Head?.BranchName,
            tabIndex + 1,
            workspace.Tabs.Length,
            mode.ToString().ToUpperInvariant(),
            pendingKeys,
            position,
            loaded,
            session?.IsLoading ?? false,
            message.Length > 0 ? message : notice,
            Tabs: tabs,
            Picker: picker,
            HomeDirectory: homeDirectory,
            Banner: OperationBannerBuilder.Build(
                tab?.Operation,
                OperationBannerBuilder.ConflictCount(tab?.WorkingTree),
                ConflictEditorSupport.AnyCanOpen(tab?.WorkingTree),
                keymap),
            TruncatedTotal: session?.TruncatedTotal);

        return screenModel;
    }

    private static string MissingNotice(RepositoryState? tab) =>
        tab is { IsMissing: true } ? $"{tab.Location.RootPath} is missing: Enter locates it, Ctrl-w c closes the tab" : string.Empty;
}
