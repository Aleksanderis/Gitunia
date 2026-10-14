using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Tabs;

namespace Gitunia.Tui.App.Tabs;

/// <summary>Builds the tab row content from the workspace: names (with the parent directory when two tabs share a name) and status markers.</summary>
public static class TabLabelFactory
{
    /// <param name="workspace">The workspace.</param>
    /// <param name="isLoading">Whether a tab is still loading in the background.</param>
    /// <param name="showBranch">Whether the checked-out branch is part of the tab (off by default, #219: it makes tabs long).</param>
    public static IReadOnlyList<TabLabel> Create(Workspace workspace, Func<TabId, bool> isLoading, bool showBranch = false)
    {
        var duplicates = workspace.Tabs
            .Where(tab => tab.CustomName is null)
            .GroupBy(tab => tab.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var labels = new List<TabLabel>(workspace.Tabs.Length);
        for (var index = 0; index < workspace.Tabs.Length; index++)
        {
            var tab = workspace.Tabs[index];
            labels.Add(new TabLabel(
                index + 1,
                tab.CustomName is null && duplicates.Contains(tab.DisplayName) ? WithParent(tab) : tab.Title,
                showBranch ? tab.Head?.BranchName : null,
                tab.Id == workspace.ActiveTabId,
                tab.Status?.IsDirty ?? false,
                tab.Status?.Ahead ?? 0,
                tab.Status?.Behind ?? 0,
                tab.IsMissing,
                !tab.IsMissing && isLoading(tab.Id)));
        }

        return labels;
    }

    private static string WithParent(RepositoryState tab)
    {
        var parent = Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(tab.Location.RootPath)) ?? string.Empty);
        return parent.Length == 0 ? tab.DisplayName : $"{tab.DisplayName} ({parent})";
    }
}
