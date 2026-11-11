using Gitunia.App;
using Gitunia.App.Shell;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Input;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App;

/// <summary>
/// Performs <see cref="RouteActionEffect"/>: offers the action to the handlers the reducer chose, in its order, until
/// one takes it. It decides nothing about who is first (that is <see cref="InputRouting"/>); it keeps the status notice
/// of the pane that took the action and starts the sticky repeat mode.
/// </summary>
/// <param name="handlers">The object behind each handler case.</param>
/// <param name="host">The state, and where the Insert mode safety net sends its command.</param>
/// <param name="repeat">Starts the sticky repeat mode after a pane took a step.</param>
/// <param name="sessions">The active tab's session, null when there is none.</param>
public sealed class ActionExecutor(
    InputHandlers handlers,
    Dispatcher host,
    IRepeatStarter repeat,
    IActiveSessions sessions,
    KeyBindingTable? keymap = null) : IActionRouteEffects
{
    private string _message = string.Empty;

    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Message => IsStaleLoadingNotice() ? string.Empty : _message;

    /// <summary>Runs the effect on the UI thread.</summary>
    /// <param name="effect">The action and the handlers that may take it.</param>
    public void RouteAction(RouteActionEffect effect)
    {
        var reachedPanes = false;
        var isTaken = false;
        foreach (var handler in effect.Handlers)
        {
            var isPaneLayer = InputRouting.IsPaneLayer(handler);
            if (isPaneLayer && !reachedPanes)
            {
                reachedPanes = true;
                _message = string.Empty;
            }

            var target = handlers.ActionsOf(handler);
            if (!target.TryExecute(effect.Action, effect.Count))
            {
                continue;
            }

            if (isPaneLayer && target is IActionNotice notice)
            {
                _message = KeyTokens.Resolve(notice.Notice, keymap);
            }

            isTaken = true;

            break;
        }

        if (!isTaken && effect.Action == ActionIds.Cancel && host.State.Shell.Mode == KeyMode.Insert)
        {
            // Insert mode nobody owns any more (#1354): Esc must always get out of it, or no key would work again.
            host.SetKeyMode(KeyMode.Normal);
        }

        if (reachedPanes)
        {
            repeat.AfterPaneAction(effect.Action);
        }
    }

    // The hint that the log is still streaming in ends with the load.
    private bool IsStaleLoadingNotice() =>
        _message == NavigationActions.StillLoadingNotice && sessions.ActiveSession()?.IsLoading != true;
}
