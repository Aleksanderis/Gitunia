using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App;

/// <summary>The refresh action: reads the active tab again.</summary>
/// <param name="host">Holds the active tab.</param>
/// <param name="effects">The runner, which refreshes a tab.</param>
public sealed class RefreshActions(Dispatcher host, EffectRunner<TerminalTabSession> effects)
    : IActionHandler, IActionNotice
{
    /// <summary>"refreshing..." or "not loaded yet", empty when the last action was not a refresh.</summary>
    public string Notice { get; private set; } = string.Empty;

    public bool TryExecute(ActionId action, int? count)
    {
        if (action != ActionIds.RepositoryRefresh)
        {
            return false;
        }

        var isRefreshing = host.Workspace.ActiveTabId is { } activeId && effects.Refresh(activeId);
        Notice = isRefreshing ? "refreshing..." : "not loaded yet";

        return true;
    }
}
