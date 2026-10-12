using Gitunia.Core.Workspaces;
using Gitunia.Tui.Rendering;

namespace Gitunia.Tui.App;

/// <summary>Builds the tab row content from the workspace: names (with the parent directory when two tabs share a name) and status markers.</summary>
public static class TabLabelFactory
{
    /// <param name="workspace">The workspace.</param>
    /// <param name="isLoading">Whether a tab is still loading in the background.</param>
    public static IReadOnlyList<TabLabel> Create(Workspace workspace, Func<TabId, bool> isLoading)
    {
        var duplicates = workspace.Tabs
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
                duplicates.Contains(tab.DisplayName) ? WithParent(tab) : tab.DisplayName,
                tab.Head?.BranchName,
                tab.Id == workspace.ActiveTabId,
                tab.Status?.IsDirty ?? false,
                tab.Status?.Ahead ?? 0,
                tab.Status?.Behind ?? 0,
                tab.IsMissing,
                !tab.IsMissing && isLoading(tab.Id)));
        }

        return labels;
    }

    private static string WithParent(RepoState tab)
    {
        var parent = Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(tab.Location.RootPath)) ?? string.Empty);
        return parent.Length == 0 ? tab.DisplayName : tab.DisplayName + " (" + parent + ")";
    }
}
