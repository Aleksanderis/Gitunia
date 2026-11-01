using Gitunia.App.Shell;
using Gitunia.App;
using Gitunia.App.Status;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// Visual mode: <c>v</c> (or a mouse drag) anchors a range at the cursor row of the graph, detail, diff or Changes view diff pane, the motions (or the
/// pointer) move the other end, and <c>v</c>, <c>Esc</c> or <c>q</c> (or a click) ends it. The range is drawn stronger than the selected row;
/// <c>y</c> copies it and ends it when the copy worked (a failed copy keeps it; in the Changes view diff the staging keys act on the selected lines). The sidebar has no ranges.
/// </summary>
/// <param name="focusedPane">The pane that has focus.</param>
/// <param name="listOf">The list of a pane.</param>
/// <param name="host">Holds the input mode, which this controller switches (Visual while a range is active).</param>
/// <param name="copyRange">Copies the rows of a range, the range ends only when it was copied; null disables <c>y</c>.</param>
/// <param name="statuses">Where the pane tells that it ended the range by itself (new rows); null tells nothing.</param>
public sealed class VisualRangeController(
    Func<PaneKind> focusedPane,
    Func<PaneKind, ListView> listOf,
    Dispatcher host,
    Func<PaneKind, RowRange, RangeCopyResult>? copyRange = null,
    StatusReporter? statuses = null)
{
    /// <summary>The status message when the pane ended the range (new rows, or a wrap, width or view change).</summary>
    public const string InvalidatedNotice = "Selection ended: the rows changed";

    private PaneKind _pane;
    private IRangeSource? _source;

    /// <summary>A range is active.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The active range, null when none.</summary>
    public RowRange? Range { get; private set; }

    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Notice { get; private set; } = string.Empty;

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (action == ActionIds.VisualToggle)
        {
            if (IsActive)
            {
                End();
            }
            else
            {
                Begin();
            }

            return true;
        }

        if (IsActive && action == ActionIds.VisualCopy)
        {
            if (Range is { } range && copyRange is not null)
            {
                var result = copyRange(_pane, range);
                Notice = result.Notice;
                if (!result.Copied)
                {
                    // Keep the range so the user can retry (or leave it with Esc).
                    return true;
                }
            }

            End();
            return true;
        }

        if (IsActive && (action == ActionIds.Cancel || action == ActionIds.NavigateBack || action == ActionIds.AppQuit))
        {
            End();
            return true;
        }

        return false;
    }

    /// <summary>Moves the far end of the range to the cursor; call when the cursor of the active pane moved.</summary>
    public void OnCursorMoved()
    {
        if (!IsActive || Range is not { } range)
        {
            return;
        }

        var list = listOf(_pane);
        if (list.SelectedItem is not { } item)
        {
            return;
        }

        Show(list, range.MoveCursorTo(item));
    }

    /// <summary>Ends the range when the focus left the pane it belongs to (another pane, another tab).</summary>
    public void EndIfFocusMoved()
    {
        if (IsActive && focusedPane() != _pane)
        {
            End();
        }
    }

    public void End()
    {
        if (!IsActive)
        {
            return;
        }

        if (_source is not null)
        {
            _source.RangeInvalidated -= OnInvalidated;
            _source.Range = null;
        }

        _source = null;
        IsActive = false;
        Range = null;
        listOf(_pane).SetNeedsDraw();
        if (host.State.Shell.Mode == KeyMode.Visual)
        {
            host.SetKeyMode(KeyMode.Normal);
        }
    }

    private void OnInvalidated()
    {
        End();
        statuses?.Post(InvalidatedNotice);
    }

    private void Begin()
    {
        if (host.State.Shell.Mode != KeyMode.Normal)
        {
            // A prompt or another modal state owns the keys: a range would strand it.
            return;
        }

        var focused = focusedPane();
        if (focused == PaneKind.Sidebar)
        {
            Notice = "No range selection in the sidebar";
            return;
        }

        var list = listOf(focused);
        if (list.SelectedItem is not { } item || list.Source is not IRangeSource rangeSource)
        {
            Notice = "Nothing to select";
            return;
        }

        _pane = focused;
        _source = rangeSource;
        rangeSource.RangeInvalidated += OnInvalidated;
        IsActive = true;
        Show(list, new RowRange(item, item));
        host.SetKeyMode(KeyMode.Visual);
    }

    private void Show(ListView list, RowRange range)
    {
        Range = range;
        if (_source is not null)
        {
            _source.Range = range;
        }

        list.SetNeedsDraw();
    }
}
