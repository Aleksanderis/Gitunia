using Gitunia.Core.Clipboard;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Conflicts;
using Gitunia.Core.FileTree;
using Gitunia.Core.ExternalTools;
using Gitunia.Core.Input;
using Gitunia.Core.Reflog;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Staging;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App;

/// <summary>
/// The effects only the terminal layer can perform (the clipboard, the terminal hand-off). The
/// runner
/// calls it on the UI thread; the terminal layer implements it. Each moves into a handler of its own when
/// its feature migrates (the clipboard: #971). The confirm dialog and the key mode are state (#970).
/// </summary>
public interface IShellEffects
{
    /// <summary>Copies the text to the clipboard and reports the outcome.</summary>
    void CopyToClipboard(CopyToClipboardEffect effect);

    /// <summary>Copies the rows of a Visual range and answers with <c>VisualCopied</c>.</summary>
    void CopyVisualRange(CopyVisualRangeEffect effect);

    /// <summary>Offers to run the same git arguments in the real terminal (credentials are needed).</summary>
    void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments);

    /// <summary>
    /// Starts the flow a reflog entry stands for (the reset prompt, the branch prompt, a detached checkout, the graph
    /// cursor) and answers with <c>ReflogEntryActionStarted</c> or <c>ReflogEntryActionRefused</c>.
    /// </summary>
    void StartReflogEntryAction(ReflogEntryActionEffect effect);

    /// <summary>
    /// Marks a saved, fully resolved conflicted file as resolved (stages it) and reports.
    /// </summary>
    void StageResolvedConflict(StageResolvedConflictEffect effect);

    /// <summary>Makes the options of the config file take effect in the running app.</summary>
    void ApplyOptions(ConfigOptions options);

    /// <summary>Shows a theme in the running app.</summary>
    void ApplyTheme(ThemeChoice theme);

    /// <summary>Opens the config file in the editor; <c>SettingsEditorClosed</c> follows when it closes.</summary>
    void OpenConfigEditor();

    /// <summary>Suspends the UI, runs the git tool on the real terminal and refreshes the tab afterwards.</summary>
    void LaunchExternalTool(LaunchExternalToolEffect effect);

    /// <summary>Moves the cursor of the Changes view diff of the tab after a staged hunk or range was read again; does
    /// nothing when the tab is not the one shown.</summary>
    void MoveDiffCursor(MoveDiffCursorEffect effect);

    /// <summary>Moves the cursor of the detail list to a row.</summary>
    void MoveDetailCursor(int row);

    /// <summary>Shows the content of a file row of the Files tab in the diff pane's place; reports why it cannot.</summary>
    void OpenFileContent(FileTreeRow row);

    /// <summary>Offers an action to its handlers in order (the terminal layer owns the lists they read).</summary>
    void RouteAction(RouteActionEffect effect);

    /// <summary>Saves the session now; false when the save failed.</summary>
    bool FlushSession();

    /// <summary>Stops the program: the main loop ends, the session is saved on the way out.</summary>
    void Quit();

    /// <summary>Moves the cursor of the list the quick jump labelled to a row and scrolls it into view.</summary>
    void JumpToRow(int row);

    /// <summary>Moves the graph cursor to a graph index and scrolls it into view.</summary>
    void SelectGraphIndex(int index);

    /// <summary>Shows the stash of a sidebar row in the detail pane.</summary>
    void ShowStash(SidebarRow row);

    /// <summary>Ends the stash view, if one is shown, and puts the graph selection back in the pane.</summary>
    void LeaveStash();

    /// <summary>Shows the stash under the sidebar cursor when a stash is shown already.</summary>
    void FollowStashCursor();
}
