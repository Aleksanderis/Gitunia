using Gitunia.Core.Keymap;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Tui.Rendering;

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
    /// <param name="picker">The open repo picker, null when closed.</param>
    /// <param name="homeDirectory">The home directory the picker abbreviates to <c>~</c>.</param>
    public static ScreenModel Create(
        Workspace workspace,
        TabSession? session,
        KeyMode mode,
        string pendingKeys,
        string message,
        int? selectedItem,
        IReadOnlyList<TabLabel>? tabs = null,
        PickerState? picker = null,
        string homeDirectory = "")
    {
        var tab = workspace.ActiveTab;
        var tabIndex = tab is null ? 0 : workspace.Tabs.IndexOf(tab);
        var loaded = session?.Graph.Count ?? 0;
        var position = selectedItem is { } item && loaded > 0 ? Math.Clamp(item - ScreenLayout.ReservedLeadingRows, 0, loaded - 1) + 1 : 0;
        return new ScreenModel(
            tab?.DisplayName ?? string.Empty,
            tab?.Head?.BranchName,
            tabIndex + 1,
            workspace.Tabs.Length,
            mode.ToString().ToUpperInvariant(),
            pendingKeys,
            position,
            loaded,
            session?.IsLoading ?? false,
            message.Length > 0 ? message : session?.LoadError is { Length: > 0 } loadError ? loadError : picker is null ? MissingNotice(tab) : string.Empty,
            Tabs: tabs,
            Picker: picker,
            HomeDirectory: homeDirectory);
    }

    private static string MissingNotice(RepoState? tab) =>
        tab is { IsMissing: true } ? $"{tab.Location.RootPath} is missing: Enter locates it, Ctrl-w c closes the tab" : string.Empty;
}
