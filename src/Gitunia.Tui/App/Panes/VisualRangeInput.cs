using Gitunia.Tui.Input;
using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// The terminal side of Visual mode: <c>v</c> (or a mouse drag) asks for a range at the cursor row, the cursor moves the
/// other end, and <c>v</c>, <c>Esc</c> or <c>q</c> (or a click) end it. <see cref="VisualReducer"/> decides; this class
/// reads the cursor, turns keys into <see cref="VisualCommand"/>s and draws the range state in the list source of the
/// pane (stronger than the selected row). <c>y</c> copies it through the shell; the sidebar has no ranges.
/// </summary>
public sealed class VisualRangeInput : IActionHandler
{
    private readonly Dispatcher _host;
    private readonly IPaneLists _lists;
    private PaneKind _pane;
    private IRangeSource? _source;

    /// <param name="host">The state and the commands.</param>
    /// <param name="lists">The lists of the panes, which own the cursors and the range sources.</param>
    public VisualRangeInput(Dispatcher host, IPaneLists lists)
    {
        _host = host;
        _lists = lists;
        host.Changed += Sync;
    }

    /// <summary>A range is active.</summary>
    public bool IsActive => _host.State.Shell.Visual is not null;

    /// <summary>The active range, null when none.</summary>
    public RowRange? Range => _host.State.Shell.Visual?.Range;

    public bool TryExecute(ActionId action, int? count)
    {
        if (action == ActionIds.VisualToggle)
        {
            Toggle();

            return true;
        }

        if (!IsActive)
        {
            return false;
        }

        if (action == ActionIds.VisualCopy)
        {
            _host.Dispatch(new VisualCopyRequested());

            return true;
        }

        if (action == ActionIds.Cancel || action == ActionIds.NavigateBack || action == ActionIds.AppQuit)
        {
            End();

            return true;
        }

        return false;
    }

    /// <summary>Moves the far end of the range to the cursor; call when the cursor of a pane moved.</summary>
    public void OnCursorMoved()
    {
        if (_host.State.Shell.Visual is not { } selection)
        {
            return;
        }

        if (_lists.ListOf(selection.Pane).SelectedItem is { } item && item != selection.Range.Cursor)
        {
            _host.Dispatch(new VisualCursorMoved(item));
        }
    }

    public void End()
    {
        if (IsActive)
        {
            _host.Dispatch(new VisualEnded());
        }
    }

    private void Toggle()
    {
        var pane = _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph;
        var list = _lists.ListOf(pane);

        _host.Dispatch(new VisualToggled(list.SelectedItem, list.Source is IRangeSource));
    }

    // Draws the range of the state: the source of the pane holds it, and tells when its rows changed meaning.
    private void Sync()
    {
        if (_host.State.Shell.Visual is not { } selection)
        {
            Detach();

            return;
        }

        if (_source is null || _pane != selection.Pane)
        {
            Detach();
            Attach(selection.Pane);
        }

        if (_source is not null && _source.Range != selection.Range)
        {
            _source.Range = selection.Range;
            _lists.ListOf(_pane).SetNeedsDraw();
        }
    }

    private void Attach(PaneKind pane)
    {
        if (_lists.ListOf(pane).Source is not IRangeSource source)
        {
            return;
        }

        _pane = pane;
        _source = source;
        source.RangeInvalidated += OnInvalidated;
    }

    private void Detach()
    {
        if (_source is null)
        {
            return;
        }

        _source.RangeInvalidated -= OnInvalidated;
        _source.Range = null;
        _source = null;
        _lists.ListOf(_pane).SetNeedsDraw();
    }

    private void OnInvalidated() => _host.Dispatch(new VisualInvalidated());
}
