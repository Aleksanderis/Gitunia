namespace Gitunia.Core.Keymap;

/// <summary>Human-readable titles of the actions, shown by the command palette, the help overlay and the which-key popup.</summary>
public static class ActionCatalog
{
    private const string TabGoToPrefix = "tab.go-to.";

    private static readonly Dictionary<string, string> Titles = new()
    {
        ["cancel"] = "Cancel",
        ["command.execute"] = "Run the command",
        ["confirm.no"] = "Answer no",
        ["confirm.yes"] = "Answer yes",
        ["detail.copy-path"] = "Copy the file path",
        ["detail.open-diff"] = "Open the file diff",
        ["detail.retry"] = "Retry loading the commit details",
        ["diff.copy-file"] = "Copy the displayed rows of the current file",
        ["diff.copy-line"] = "Copy the current diff line",
        ["diff.load-anyway"] = "Load the whole diff (lift the size limits)",
        ["diff.next-file"] = "Next file",
        ["diff.next-hunk"] = "Next hunk",
        ["diff.previous-file"] = "Previous file",
        ["diff.previous-hunk"] = "Previous hunk",
        ["diff.search-next"] = "Next diff search match",
        ["diff.search-open"] = "Search in the diff",
        ["diff.search-previous"] = "Previous diff search match",
        ["diff.toggle-wrap"] = "Toggle word wrap in the diff",
        ["graph.copy-hash"] = "Copy the commit hash",
        ["graph.jump-to-head"] = "Jump to HEAD",
        ["graph.jump-to-ref"] = "Jump to a ref or hash",
        ["graph.open-details"] = "Focus the commit details",
        ["help.show"] = "Show keys for this view",
        ["input.accept"] = "Accept the input",
        ["list.bottom"] = "Go to the bottom",
        ["list.down"] = "Move down",
        ["list.half-page-down"] = "Half page down",
        ["list.half-page-up"] = "Half page up",
        ["list.top"] = "Go to the top",
        ["list.up"] = "Move up",
        ["navigate.back"] = "Go back or quit",
        ["palette.next"] = "Next palette entry",
        ["palette.open"] = "Open the command palette",
        ["palette.previous"] = "Previous palette entry",
        ["pane.focus-down"] = "Focus the pane below",
        ["pane.focus-left"] = "Focus the pane to the left",
        ["pane.focus-right"] = "Focus the pane to the right",
        ["pane.focus-up"] = "Focus the pane above",
        ["pane.next"] = "Next pane",
        ["pane.previous"] = "Previous pane",
        ["pane.narrower"] = "Make the focused pane narrower",
        ["pane.reset-sizes"] = "Reset the pane sizes",
        ["pane.shorter"] = "Make the focused pane shorter",
        ["pane.taller"] = "Make the focused pane taller",
        ["pane.wider"] = "Make the focused pane wider",
        ["pane.zoom"] = "Zoom the focused pane",
        ["picker.accept"] = "Open the repository",
        ["picker.complete-path"] = "Complete the path",
        ["picker.next"] = "Next picker entry",
        ["picker.open"] = "Open the repository picker",
        ["picker.pin"] = "Pin or unpin the entry",
        ["picker.previous"] = "Previous picker entry",
        ["picker.remove-recent"] = "Remove from recents",
        ["repo.pin"] = "Pin or unpin the repository",
        ["repo.refresh"] = "Refresh the repository",
        ["search.next"] = "Next search match",
        ["search.open"] = "Search",
        ["search.previous"] = "Previous search match",
        ["sidebar.activate"] = "Activate the sidebar entry",
        ["sidebar.collapse"] = "Collapse the sidebar entry",
        ["sidebar.copy"] = "Copy the branch, tag, stash name or worktree path",
        ["sidebar.expand"] = "Expand the sidebar entry",
        ["sidebar.filter"] = "Filter the sidebar",
        ["status.dismiss"] = "Dismiss the status message",
        ["status.history"] = "Show the status message history",
        ["tab.close"] = "Close the tab",
        ["tab.last-active"] = "Previously active tab",
        ["tab.next"] = "Next tab",
        ["tab.previous"] = "Previous tab",
        ["visual.toggle"] = "Start or end range selection",
    };

    /// <summary>The title of <paramref name="action"/>; the id itself when it has none (a test keeps the defaults covered).</summary>
    public static string Describe(ActionId action)
    {
        if (Titles.TryGetValue(action.Value, out var title))
        {
            return title;
        }

        return action.Value.StartsWith(TabGoToPrefix, StringComparison.Ordinal)
            ? "Go to tab " + action.Value[TabGoToPrefix.Length..]
            : action.Value;
    }

    /// <summary>True when <paramref name="action"/> has a real title.</summary>
    public static bool IsDescribed(ActionId action) => Describe(action) != action.Value;
}
