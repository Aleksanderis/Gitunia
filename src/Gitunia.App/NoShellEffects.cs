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

/// <summary>The shell of a runner without a terminal: nothing happens.</summary>
public sealed class NoShellEffects : IShellEffects
{
    public static NoShellEffects Instance { get; } = new();

    public void RouteAction(RouteActionEffect effect)
    {
    }

    public void CopyToClipboard(CopyToClipboardEffect effect)
    {
    }

    public void OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments)
    {
    }

    public void RunTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments)
    {
    }

    public void StartReflogEntryAction(ReflogEntryActionEffect effect)
    {
    }

    public void StageResolvedConflict(StageResolvedConflictEffect effect)
    {
    }

    public void ApplyOptions(ConfigOptions options)
    {
    }

    public void ApplyTheme(ThemeChoice theme)
    {
    }

    public void CopyVisualRange(CopyVisualRangeEffect effect)
    {
    }

    public void OpenConfigEditor()
    {
    }

    public void LaunchExternalTool(LaunchExternalToolEffect effect)
    {
    }

    public void MoveDiffCursor(MoveDiffCursorEffect effect)
    {
    }

    public void MoveDetailCursor(int row)
    {
    }

    public void OpenFileContent(FileTreeRow row)
    {
    }

    public bool FlushSession() => true;

    public void Quit()
    {
    }

    public void JumpToRow(int row)
    {
    }

    public void SelectGraphIndex(int index)
    {
    }

    public void ShowStash(SidebarRow row)
    {
    }

    public void LeaveStash()
    {
    }

    public void FollowStashCursor()
    {
    }
}
