using Gitunia.App;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Conflicts;
using Gitunia.Core.ExternalTools;
using Gitunia.Core.FileTree;
using Gitunia.Core.Input;
using Gitunia.Core.Reflog;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Staging;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.Shell;

/// <summary>The effects only the terminal layer can perform, handed to the groups that own them.</summary>
/// <param name="parts">The groups of effects.</param>
/// <param name="actions">Offers a routed action to its handlers.</param>
public sealed class ShellEffects(ShellEffectParts parts, ActionExecutor actions) : IShellEffects
{
    /// <inheritdoc />
    public void CopyToClipboard(CopyToClipboardEffect effect) => parts.Copy.CopyToClipboard(effect);

    /// <inheritdoc />
    public void CopyVisualRange(CopyVisualRangeEffect effect) => parts.Copy.CopyVisualRange(effect);

    /// <inheritdoc />
    public void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments) =>
        parts.Copy.OfferTerminalRetry(tabId, noun, arguments);

    public void RunTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments) =>
        parts.Copy.RunTerminalRetry(tabId, noun, arguments);

    /// <inheritdoc />
    public void StartReflogEntryAction(ReflogEntryActionEffect effect) => parts.Flow.StartReflogEntryAction(effect);

    /// <inheritdoc />
    public void StageResolvedConflict(StageResolvedConflictEffect effect) => parts.Flow.StageResolvedConflict(effect);

    /// <inheritdoc />
    public void ApplyOptions(ConfigOptions options) => parts.Session.ApplyOptions(options);

    /// <inheritdoc />
    public void ApplyTheme(ThemeChoice theme) => parts.Session.ApplyTheme(theme);

    /// <inheritdoc />
    public void OpenConfigEditor() => parts.Tools.OpenConfigEditor();

    /// <inheritdoc />
    public void LaunchExternalTool(LaunchExternalToolEffect effect) => parts.Tools.Launch(effect);

    /// <inheritdoc />
    public void MoveDiffCursor(MoveDiffCursorEffect effect) => parts.Cursor.MoveDiffCursor(effect);

    /// <inheritdoc />
    public void MoveDetailCursor(int row) => parts.Cursor.MoveDetailCursor(row);

    /// <inheritdoc />
    public void OpenFileContent(FileTreeRow row) => parts.Flow.OpenFileContent(row);

    /// <inheritdoc />
    public void RouteAction(RouteActionEffect effect) => actions.Run(effect);

    /// <inheritdoc />
    public bool FlushSession() => parts.Session.Flush();

    /// <inheritdoc />
    public void Quit() => parts.Session.Quit();

    /// <inheritdoc />
    public void JumpToRow(int row) => parts.Cursor.JumpToRow(row);

    /// <inheritdoc />
    public void SelectGraphIndex(int index) => parts.Cursor.SelectGraphIndex(index);

    /// <inheritdoc />
    public void ShowStash(SidebarRow row) => parts.Stash.Show(row);

    /// <inheritdoc />
    public void LeaveStash() => parts.Stash.Leave();

    /// <inheritdoc />
    public void FollowStashCursor() => parts.Stash.FollowCursor();
}
