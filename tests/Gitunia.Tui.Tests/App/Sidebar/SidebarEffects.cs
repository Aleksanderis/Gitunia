using Gitunia.App;
using Gitunia.App.Sidebar;
using Gitunia.App.Tabs;
using Gitunia.Core;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Effects;
using Gitunia.Core.Sidebar;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Stash;

namespace Gitunia.Tui.Tests.App.Sidebar;

/// <summary>
/// Does what the effect runner and the terminal shell do for the sidebar effects, so a test drives the real path from
/// the key to the copied text, the shown stash or the graph item: the clipboard, the graph lookup, the stash view and
/// the status messages. What it saw is kept for the assertions.
/// </summary>
public sealed class SidebarEffects
{
    private readonly Dispatcher _host;
    private readonly ClipboardCopier _copier;
    private readonly SidebarEffectHandler _locator;

    public SidebarEffects(Dispatcher host, ITabSessions sessions, ClipboardCopier copier)
    {
        _host = host;
        _copier = copier;
        _locator = new SidebarEffectHandler(sessions, host);
        host.EffectRaised += Run;
    }

    /// <summary>The stash view of the shell; null when it has none.</summary>
    public StashViewController? StashView { get; set; }

    /// <summary>The last status message since <see cref="Reset"/>, empty when none.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>The graph item (graph index) the cursor was moved to, null when none.</summary>
    public int? SelectedGraphItem { get; private set; }

    /// <summary>Forgets the message of the last action.</summary>
    public void Reset() => Message = string.Empty;

    private void Run(AppEffect effect)
    {
        switch (effect)
        {
            case CopyToClipboardEffect copy:
                _copier.Copy(copy.Text, copy.Description);
                break;
            case FollowStashCursorEffect:
                FollowStashCursor();
                break;
            case LeaveStashEffect:
                StashView?.Leave();
                break;
            case LocateGraphCommitEffect locate:
                _locator.Locate(locate);
                break;
            case PostStatusEffect post:
                Message = post.Text;
                break;
            case SelectGraphIndexEffect select:
                SelectedGraphItem = select.Index;
                break;
            case ShowStashEffect show:
                StashView?.Show(show.Row);
                break;
        }
    }

    private void FollowStashCursor()
    {
        if (StashView is { IsShowing: true } view
            && _host.Workspace.ActiveTab is { } tab
            && SidebarActionReducer.CursorRow(tab) is { Kind: SidebarRowKind.Stash } row)
        {
            view.Show(row);
        }
    }
}
