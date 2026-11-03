using System.Collections.Immutable;
using Gitunia.App;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// The view side of the repository picker: reads the session from the shell state and turns typed keys, clicks and the
/// scan folders of the settings into <see cref="PickerCommand"/>s. It owns no state and decides nothing; what a key
/// means is the picker reducer (Core).
/// </summary>
/// <param name="host">Holds the state and takes the commands.</param>
public sealed class PickerInput(Dispatcher host)
{
    /// <summary>Whether the picker is open.</summary>
    public bool IsOpen => host.State.Shell.Picker.IsOpen;

    /// <summary>The open picker, null when closed.</summary>
    public PickerState? State => host.State.Shell.Picker.State;

    /// <summary>The folders the next scan looks in, null for the conventional ones.</summary>
    public ImmutableArray<string>? ScanRoots => host.State.Shell.Picker.ScanRoots;

    /// <summary>Opens the picker, or resets it when it is open.</summary>
    /// <param name="locatingTabId">The missing tab the chosen repository replaces.</param>
    /// <param name="initialQuery">Text to start with.</param>
    public void Open(TabId? locatingTabId = null, string initialQuery = "") =>
        host.Dispatch(new PickerOpenRequested(locatingTabId, initialQuery, host.Clock.GetUtcNow()));

    /// <summary>
    /// The picker's query is plain state, not a text widget: it takes printable keys and edits itself like every single-line prompt
    /// (caret, Backspace, Delete, Home, End, word keys). Nothing is typed while a question of the picker (clone, init) is open.
    /// </summary>
    /// <param name="key">The typed key.</param>
    public void TypeKey(KeyEvent key)
    {
        if (!IsOpen || host.State.Shell.Prompt is not null)
        {
            return;
        }

        host.Dispatch(new EditQueryCommand(key));
    }

    /// <summary>A click highlights the row; the double-click's "open" comes from the mouse map as picker.accept.</summary>
    /// <param name="index">The clicked row.</param>
    public void ClickRow(int index)
    {
        if (State is { } state)
        {
            host.Dispatch(new MoveHighlightCommand(index - state.Highlight));
        }
    }

    /// <summary>The settings changed the folders to scan.</summary>
    /// <param name="roots">The folders, null for the conventional ones.</param>
    public void ChangeScanRoots(ImmutableArray<string>? roots) =>
        host.Dispatch(new PickerScanRootsChanged(roots));
}
