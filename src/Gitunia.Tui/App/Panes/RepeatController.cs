using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap.Repeat;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// Holds the sticky repeat mode (docs/ux.md, "Modes"): started after a step action, it gives the step keys of the
/// keymap views <c>ContextRepeat</c> and <c>ResizeRepeat</c> priority over the pane's own keys until another key,
/// a focus change or an overlay ends it.
/// </summary>
/// <param name="table">The key bindings, which hold the keys of the repeat views.</param>
/// <param name="activeTab">The active tab.</param>
/// <param name="focusedPane">The pane that has focus.</param>
/// <param name="isBlocked">An overlay, prompt or search owns the keys.</param>
/// <param name="execute">Runs a step action, as the key binding would.</param>
public sealed class RepeatController(
    KeyBindingTable table,
    Func<TabId?> activeTab,
    Func<PaneKind> focusedPane,
    Func<bool> isBlocked,
    Action<ActionId, int?> execute)
{
    private RepeatMode? _mode;

    /// <summary>The mode that is on, null when none or when its tab or pane is no longer the active one.</summary>
    public RepeatMode? Active
    {
        get
        {
            if (_mode is { } mode && (!mode.AppliesTo(activeTab(), focusedPane()) || isBlocked()))
            {
                _mode = null;
            }

            return _mode;
        }
    }

    /// <summary>Starts the mode that <paramref name="action"/> belongs to, if any; call it after the action ran.</summary>
    /// <param name="action">The action that just ran.</param>
    public void Started(ActionId action)
    {
        if (activeTab() is { } tab && RepeatMode.Start(action, tab, focusedPane()) is { } started)
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

        execute(binding.Action, null);

        return true;
    }
}
