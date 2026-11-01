using Gitunia.Core.Clipboard;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Reflog;
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

    /// <summary>Offers to run the same git arguments in the real terminal (credentials are needed).</summary>
    void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments);

    /// <summary>
    /// Starts the flow a reflog entry stands for (the reset prompt, the branch prompt, a detached checkout, the graph
    /// cursor) and answers with <c>ReflogEntryActionStarted</c> or <c>ReflogEntryActionRefused</c>.
    /// </summary>
    void StartReflogEntryAction(ReflogEntryActionEffect effect);

    /// <summary>
    /// Marks a saved, fully resolved conflicted file as resolved (stages it) and reports; staging has not moved to
    /// commands yet.
    /// </summary>
    void StageResolvedConflict(StageResolvedConflictEffect effect);

    /// <summary>Makes the options of the config file take effect in the running app.</summary>
    void ApplyOptions(ConfigOptions options);

    /// <summary>Shows a theme in the running app.</summary>
    void ApplyTheme(ThemeChoice theme);

    /// <summary>Opens the config file in the editor; <c>SettingsEditorClosed</c> follows when it closes.</summary>
    void OpenConfigEditor();
}
