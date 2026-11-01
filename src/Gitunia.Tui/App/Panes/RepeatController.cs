using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Tui.Input;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap.Repeat;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// Holds the sticky repeat mode (docs/internal/ux.md, "Modes"): started after a step action, it gives the step keys of the
/// keymap views <c>ContextRepeat</c> and <c>ResizeRepeat</c> priority over the pane's own keys until another key,
/// a focus change or an overlay ends it.
/// </summary>
/// <param name="table">The key bindings, which hold the keys of the repeat views.</param>
/// <param name="host">Holds the active tab and its focused pane.</param>
/// <param name="blocker">Tells when an overlay, prompt or search owns the keys.</param>
/// <param name="route">Runs a step action, as the key binding would.</param>
public sealed class RepeatController(KeyBindingTable table, Dispatcher host, IInputBlocker blocker, IActionRoute route)
    : IKeyInterceptor
{
    private RepeatMode? _mode;

    /// <summary>The mode that is on, null when none or when its tab or pane is no longer the active one.</summary>
    public RepeatMode? Active
    {
        get
        {
            if (_mode is { } mode && (!mode.AppliesTo(host.Workspace.ActiveTabId, FocusedPane()) || blocker.IsBlocked))
            {
                _mode = null;
            }

            return _mode;
        }
    }

    /// <summary>Starts the mode that <paramref name="action"/> belongs to, if any; call after the action ran.</summary>
    /// <param name="action">The action that just ran.</param>
    public void Started(ActionId action)
    {
        if (host.Workspace.ActiveTabId is { } tab && RepeatMode.Start(action, tab, FocusedPane()) is { } started)
        {
            _mode = started;
        }
    }

    /// <summary>Ends the mode.</summary>
    public void End() => _mode = null;

    /// <summary>
    /// Gives the mode the first try at a key: a step key (or <c>Esc</c>) is taken, any other key ends the mode and
    /// is left to the normal key handling.
    /// </summary>
    /// <param name="key">The key press.</param>
    /// <param name="state">The keymap state before the key.</param>
    /// <returns>True when the mode consumed the key.</returns>
    public bool TryHandle(KeyEvent key, KeymapState state)
    {
        if (Active is not { } mode)
        {
            return false;
        }

        if (state.Mode != KeyMode.Normal || state.HasPending || state.Count is not null)
        {
            End();

            return false;
        }

        var sequence = KeySequence.Empty.Append(key.Normalize());
        var binding = table.Bindings.FirstOrDefault(candidate =>
            candidate.View == mode.View && candidate.Mode == KeyMode.Normal && candidate.Sequence.Equals(sequence));
        if (binding is null)
        {
            End();

            return false;
        }

        if (binding.Action == ActionIds.RepeatEnd)
        {
            End();

            return true;
        }

        route.Route(binding.Action, null);

        return true;
    }

    private PaneKind FocusedPane() => host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph;
}
