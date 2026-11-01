using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Tui.App.Conflicts;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// Which overlays are open and which of them covers another: the two editors sit under the dialogs and lists, and the
/// rebase editor under the conflict editor's confirm too. Tells the mouse gate whenever the answer may have changed.
/// </summary>
public sealed class OverlayCover : IConflictCover, IOverlayCover
{
    private readonly OverlayStore _store;
    private readonly OtherOverlays _others;
    private readonly RebaseEditorInput _rebaseEditor;
    private readonly ConflictEditorInput _conflictEditor;
    private readonly MouseGate _gate;

    /// <param name="store">The help, palette, menu and confirm dialog.</param>
    /// <param name="others">The overlays that cover the editors.</param>
    /// <param name="editors">The two editors.</param>
    /// <param name="host">Raises the changes the gate follows.</param>
    /// <param name="gate">Told whether any overlay is open.</param>
    public OverlayCover(
        OverlayStore store,
        OtherOverlays others,
        EditorOverlays editors,
        Dispatcher host,
        MouseGate gate)
    {
        _store = store;
        _others = others;
        _rebaseEditor = editors.Rebase;
        _conflictEditor = editors.Conflict;
        _gate = gate;
        host.Changed += UpdateGate;
        store.Changed += UpdateGate;
    }

    /// <summary>Any overlay, editor or dialog is open.</summary>
    public bool IsAnyOverlayOpen() => _rebaseEditor.IsOpen || _conflictEditor.IsOpen || _others.IsOtherOverlayOpen();

    /// <summary>The overlays a mainline question or a prompt popup hides behind.</summary>
    public bool IsMainlineCovered() => _others.IsHidingMainline || _rebaseEditor.IsOpen || _conflictEditor.IsOpen;

    /// <inheritdoc />
    public bool IsOtherOverlayOpen() => _others.IsOtherOverlayOpen();

    /// <inheritdoc />
    public bool IsOtherThanConflictEditorOpen() => _rebaseEditor.IsOpen || _others.IsOtherOverlayOpen();

    /// <summary>The keymap view of the overlay that has the keys, or the focused pane's when none is open.</summary>
    public string ViewName()
    {
        if (_others.IsSettingsOpen)
        {
            return DefaultKeymap.SettingsView;
        }

        if (_conflictEditor.IsOpen && !_store.State.IsOpen)
        {
            return _conflictEditor.ViewName;
        }

        if (_rebaseEditor.IsOpen && !_store.State.IsOpen)
        {
            return DefaultKeymap.RebaseEditorView;
        }

        return _others.IsReflogOpen ? DefaultKeymap.ReflogView : _store.ViewName;
    }

    private void UpdateGate() => _gate.Set(IsAnyOverlayOpen());
}
