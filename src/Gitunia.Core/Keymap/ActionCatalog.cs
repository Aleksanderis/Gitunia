using System.Collections.Frozen;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Keymap;

/// <summary>Human-readable titles of the actions, shown by the command palette, the help overlay and the which-key popup.</summary>
public static class ActionCatalog
{
    private const string TabNextChord = "gt";
    private const string TabGoToPrefix = "tab.go-to.";

    // Actions that only mean something inside a modal prompt (confirm dialog, picker, palette, text input): the palette hides them.
    private static readonly HashSet<ActionId> ModalOnly =
    [
        ActionIds.Cancel,
        ActionIds.CommandExecute,
        ActionIds.ConfirmNo,
        ActionIds.ConfirmYes,
        ActionIds.InputAccept,
        ActionIds.PaletteNext,
        ActionIds.PalettePrevious,
        ActionIds.PickerAccept,
        ActionIds.PickerCompletePath,
        ActionIds.PickerNext,
        ActionIds.PickerPin,
        ActionIds.PickerPrevious,
        ActionIds.PickerRemoveRecent,
        ActionIds.WorkingTreeMessageNewLine,
    ];

    private static readonly Dictionary<string, string> Titles = new()
    {
        ["cancel"] = "Cancel",
        ["command.execute"] = "Run the command",
        ["confirm.no"] = "Answer no",
        ["confirm.yes"] = "Answer yes",
        ["detail.copy-path"] = "Copy the file path",
        ["detail.next-tab"] = "Next bottom pane tab",
        ["detail.open-diff"] = "Go to the file diff",
        ["detail.previous-tab"] = "Previous bottom pane tab",
        ["detail.retry"] = "Retry loading the commit details",
        ["detail.tab-changes"] = "Show the Changes tab",
        ["detail.tab-details"] = "Show the Details tab",
        ["detail.tab-files"] = "Show the Files tab",
        ["diff.copy-file"] = "Copy the displayed rows of the current file",
        ["diff.copy-line"] = "Copy the current diff line",
        ["diff.load-anyway"] = "Load the whole diff (lift the size limits)",
        ["diff.less-context"] = "Show fewer lines around the changes in the diff",
        ["diff.more-context"] = "Show more lines around the changes in the diff",
        ["diff.next-file"] = "Next file",
        ["diff.next-hunk"] = "Next hunk",
        ["diff.previous-file"] = "Previous file",
        ["diff.previous-hunk"] = "Previous hunk",
        ["diff.search-next"] = "Next diff search match",
        ["diff.search-open"] = "Search in the diff",
        ["diff.search-previous"] = "Previous diff search match",
        ["diff.toggle-ignore-whitespace"] = "Toggle ignoring whitespace changes in the diff",
        ["diff.toggle-side-by-side"] = "Toggle the side-by-side diff",
        ["diff.toggle-whole-file"] = "Toggle showing whole files in the diff",
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
        ["app.quit"] = "Quit",
        ["navigate.back"] = "Go back",
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
        ["picker.complete-path"] = "Accept the suggestion / complete the path",
        ["picker.next"] = "Next picker entry",
        ["picker.open"] = "Open the repository picker",
        ["picker.pin"] = "Pin to the top of the picker (toggle)",
        ["picker.previous"] = "Previous picker entry",
        ["picker.remove-recent"] = "Remove from recents",
        ["repo.pin"] = "Pin to the top of the picker (toggle)",
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
        ["tab.rename"] = "Rename the tab",
        ["tool.commit-editor"] = "Commit the staged changes in $EDITOR",
        ["tool.difftool"] = "Open the commit in the difftool",
        ["tool.mergetool"] = "Resolve conflicts in the mergetool",
        ["view.quick-jump-to-row"] = "Quick jump to a visible row",
        ["visual.copy"] = "Copy the selected range",
        ["visual.toggle"] = "Start or end range selection",
        ["working-tree.copy-path"] = "Copy the file path",
        ["working-tree.focus-unstaged"] = "Focus the Unstaged list",
        ["working-tree.focus-staged"] = "Focus the Staged list",
        ["working-tree.focus-diff"] = "Focus the diff",
        ["working-tree.focus-message"] = "Focus the commit message",
        ["working-tree.open-diff"] = "Go to the diff of the file",
        ["working-tree.open"] = "Open the working tree",
        ["working-tree.toggle"] = "Open or leave the working tree",
        ["working-tree.stage"] = "Stage the file, hunk or selected lines",
        ["working-tree.stage-all"] = "Stage all files",
        ["working-tree.unstage"] = "Unstage the file, hunk or selected lines",
        ["working-tree.unstage-all"] = "Unstage all files",
        ["working-tree.discard"] = "Discard the file's changes, the hunk or the selected lines (asks first)",
        ["working-tree.write-message"] = "Write the commit message",
        ["working-tree.edit-message"] = "Edit the commit message",
        ["working-tree.message-new-line"] = "Start a new line in the commit message",
        ["working-tree.commit"] = "Commit the staged changes",
        ["working-tree.toggle-sign-off"] = "Commit option: SignOff (toggle)",
        ["working-tree.toggle-no-verify"] = "Commit option: No-Verify (toggle)",
        ["working-tree.toggle-amend"] = "Commit option: Amend (toggle)",
    };

    /// <summary>Actions that are bound but have no behaviour yet; the palette shows them greyed out until their milestone. Empty today: add an id here when a binding lands ahead of its feature, remove it when the feature does.</summary>
    public static IReadOnlySet<ActionId> Unimplemented { get; } = FrozenSet<ActionId>.Empty;

    /// <summary>True for actions that only make sense inside a modal prompt; the palette does not list them.</summary>
    public static bool IsModalOnly(ActionId action) => ModalOnly.Contains(action);

    /// <summary>The title of <paramref name="action"/>; the id itself when it has none (a test keeps the defaults covered).</summary>
    public static string Describe(ActionId action)
    {
        if (Titles.TryGetValue(action.Value, out var title))
        {
            return title;
        }

        if (!action.Value.StartsWith(TabGoToPrefix, StringComparison.Ordinal))
        {
            return action.Value;
        }

        var number = action.Value[TabGoToPrefix.Length..];
        return $"Go to tab {number} (same as {number}{TabNextChord})";
    }

    /// <summary>True when <paramref name="action"/> has a real title.</summary>
    public static bool IsDescribed(ActionId action) => Describe(action) != action.Value;
}
