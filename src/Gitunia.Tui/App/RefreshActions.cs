using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Shell;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App;

/// <summary>The refresh action: reads the active tab again.</summary>
/// <param name="host">Holds the active tab; the refresh goes through it.</param>
/// <param name="sessions">The active tab's session, which tells whether the tab has started loading.</param>
public sealed class RefreshActions(Dispatcher host, IActiveSessions sessions) : IActionHandler, IActionNotice
{
    /// <summary>"refreshing..." or "not loaded yet", empty when the last action was not a refresh.</summary>
    public string Notice { get; private set; } = string.Empty;

    public bool TryExecute(ActionId action, int? count)
    {
        if (action != ActionIds.RepositoryRefresh)
        {
            return false;
        }

        var isRefreshing = false;
        if (host.Workspace.ActiveTabId is { } activeId && sessions.ActiveSession() is { IsPending: false })
        {
            host.Dispatch(new TabRefreshRequested(activeId, true));
            isRefreshing = true;
        }

        Notice = isRefreshing ? "refreshing..." : "not loaded yet";

        return true;
    }
}
