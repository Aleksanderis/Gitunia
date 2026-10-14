using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// Visual mode: <c>v</c> (or a mouse drag) anchors a range at the cursor row of the graph, detail or diff pane, the motions (or the
/// pointer) move the other end, and <c>v</c>, <c>Esc</c> or <c>q</c> (or a click) ends it. The range is drawn like the selected row;
/// <c>y</c> copies it and ends it when the copy worked (a failed copy keeps it; staging lines will act on it in M2). The sidebar has no ranges.
/// </summary>
/// <param name="focusedPane">The pane that has focus.</param>
/// <param name="listOf">The list of a pane.</param>
/// <param name="currentMode">The input mode now.</param>
/// <param name="setMode">Switches the input mode (Visual while a range is active).</param>
/// <param name="copyRange">Copies the rows of a range, the range ends only when it was copied; null disables <c>y</c>.</param>
public sealed class VisualRangeController(Func<PaneKind> focusedPane, Func<PaneKind, ListView> listOf, Func<KeyMode> currentMode, Action<KeyMode> setMode, Func<PaneKind, RowRange, RangeCopyResult>? copyRange = null)
{
    private PaneKind pane;
    private IRangeSource? source;

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
                var result = copyRange(pane, range);
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

        if (IsActive && (action == ActionIds.Cancel || action == ActionIds.NavigateBack))
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

        var list = listOf(pane);
        if (list.SelectedItem is not { } item)
        {
            return;
        }

        Show(list, range.MoveCursorTo(item));
    }

    /// <summary>Ends the range when the focus left the pane it belongs to (another pane, another tab).</summary>
    public void EndIfFocusMoved()
    {
        if (IsActive && focusedPane() != pane)
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

        if (source is not null)
        {
            source.RangeInvalidated -= End;
            source.Range = null;
        }

        source = null;
        IsActive = false;
        Range = null;
        listOf(pane).SetNeedsDraw();
        if (currentMode() == KeyMode.Visual)
        {
            setMode(KeyMode.Normal);
        }
    }

    private void Begin()
    {
        if (currentMode() != KeyMode.Normal)
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

        pane = focused;
        source = rangeSource;
        rangeSource.RangeInvalidated += End;
        IsActive = true;
        Show(list, new RowRange(item, item));
        setMode(KeyMode.Visual);
    }

    private void Show(ListView list, RowRange range)
    {
        Range = range;
        if (source is not null)
        {
            source.Range = range;
        }

        list.SetNeedsDraw();
    }
}
