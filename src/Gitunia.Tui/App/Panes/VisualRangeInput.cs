using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Input;
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
    private TextPosition? _pointed;
    private bool _pointedIsOldSide;
    private TextPosition? _dragAnchor;

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

    /// <summary>Whether <paramref name="action"/> is one of the Visual mode actions this class runs.</summary>
    /// <param name="action">The action.</param>
    public static bool IsVisualAction(ActionId action) =>
        action == ActionIds.VisualToggle || action == ActionIds.VisualCopy || VisualActions.IsTextAction(action);

    public bool TryExecute(ActionId action, int? count)
    {
        if (action == ActionIds.VisualToggle)
        {
            Toggle();

            return true;
        }

        if (VisualActions.IsTextAction(action))
        {
            return TryExecuteText(action, count);
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

    /// <summary>Takes a pointer event on the text of the focused pane: a drag selects characters, a double or triple click is remembered for its action.</summary>
    /// <param name="pointed">What the pointer did and where.</param>
    public void OnTextPointed(TextPointer pointed)
    {
        switch (pointed.Kind)
        {
            case TextPointerKind.DragStart:
                _dragAnchor = pointed.Origin ?? pointed.Position;
                SelectChars(_dragAnchor.Value, pointed.Position, pointed.IsOldSide);
                break;
            case TextPointerKind.DragMove when _dragAnchor is { } anchor:
                SelectChars(anchor, pointed.Position, pointed.IsOldSide);
                break;
            case TextPointerKind.Click:
                _pointed = null;
                break;
            default:
                _pointed = pointed.Position;
                _pointedIsOldSide = pointed.IsOldSide;
                break;
        }
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
        // The pointed position stays: the gesture router ends the range before the double-click action selects at it.
        if (IsActive)
        {
            _host.Dispatch(new VisualEnded());
        }
    }

    private bool TryExecuteText(ActionId action, int? count)
    {
        if (action == ActionIds.VisualToggleChars)
        {
            ToggleChars();

            return true;
        }

        if (action == ActionIds.VisualSelectWord || action == ActionIds.VisualSelectLine)
        {
            SelectTextAt(action == ActionIds.VisualSelectWord);

            return true;
        }

        if (_host.State.Shell.Visual is { Kind: VisualKind.Chars } selection
            && TextOfPane(selection.Pane) is { } source)
        {
            var text = source.TextOf(selection.Range.Cursor, selection.IsOldSide);
            var column = VisualActions.MoveColumn(action, text, selection.CursorColumn, count ?? 1);
            _host.Dispatch(new VisualColumnMoved(column));
            FollowColumn(selection, source, column);
        }

        return IsActive;
    }

    private void ToggleChars()
    {
        var pane = _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph;
        var list = _lists.ListOf(pane);
        var source = TextOfPane(pane);
        if (source is null || list.SelectedItem is not { } item)
        {
            _host.Dispatch(new VisualCharsToggled(list.SelectedItem, 0, false));

            return;
        }

        var isOldSide = StartsOnOldSide(source, item);
        _host.Dispatch(new VisualCharsToggled(item, source.StartAt(item, isOldSide).Column, true, isOldSide));
    }

    // Side by side the cursor line selects on the new side, or on the old one when only that side has a row there.
    private static bool StartsOnOldSide(ITextSource source, int item) =>
        source.TextOf(item, false).Length == 0 && source.TextOf(item, true).Length > 0;

    // A wrapped row spreads over several lines: the cursor line goes to the one that shows the cursor column.
    private void FollowColumn(VisualSelection selection, ITextSource source, int column)
    {
        var position = new TextPosition(selection.Range.Cursor, column);
        var item = source.ItemShowing(position, selection.IsOldSide);
        var list = _lists.ListOf(selection.Pane);
        if (item != selection.Range.Cursor && list.SelectedItem != item)
        {
            list.SelectedItem = item;
        }
    }

    // A double or triple click selects at the pointer; the keys and the palette select at the cursor (its column in a
    // character-wise selection, else the start of the line).
    private void SelectTextAt(bool isWord)
    {
        var pane = _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph;
        var list = _lists.ListOf(pane);
        var source = TextOfPane(pane);
        if (source is null || (list.SelectedItem ?? _pointed?.Line) is not { } row)
        {
            _host.Dispatch(new VisualCharsToggled(list.SelectedItem, 0, false));

            return;
        }

        var selected = _host.State.Shell.Visual is { Kind: VisualKind.Chars } active ? active : null;
        var isOldSide = SideOfSelection(source, row, selected);
        var current = selected is null
            ? source.StartAt(row, isOldSide)
            : new TextPosition(selected.Range.Cursor, selected.CursorColumn);
        var at = _pointed ?? current;
        isOldSide = _pointed is null ? isOldSide : _pointedIsOldSide;
        _pointed = null;
        var text = source.TextOf(at.Line, isOldSide);
        var span = isWord ? TextMotions.WordAt(text, at.Column) : new ColumnSpan(0, TextMotions.LineEnd(text));
        SelectChars(new TextPosition(at.Line, span.First), new TextPosition(at.Line, span.Last), isOldSide);
    }

    private bool SideOfSelection(ITextSource source, int row, VisualSelection? selected) =>
        _pointed is not null ? _pointedIsOldSide : selected?.IsOldSide ?? StartsOnOldSide(source, row);

    private void SelectChars(TextPosition anchor, TextPosition cursor, bool isOldSide) =>
        _host.Dispatch(new VisualCharsSelected(anchor, cursor, isOldSide));

    private ITextSource? TextOfPane(PaneKind pane) =>
        _lists.ListOf(pane).Source is ITextSource { SupportsText: true } source ? source : null;

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

        if (_source is not { } source)
        {
            return;
        }

        var text = source as ITextSource;
        if (source.Range != selection.Range || text?.Text != selection.Text || text?.IsOldSide != selection.IsOldSide)
        {
            source.Range = selection.Range;
            SetText(selection.Text, selection.IsOldSide);
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
        SetText(null, false);
        _source = null;
        _lists.ListOf(_pane).SetNeedsDraw();
    }

    private void SetText(TextSelection? text, bool isOldSide)
    {
        if (_source is ITextSource source)
        {
            source.Text = text;
            source.IsOldSide = isOldSide;
        }
    }

    private void OnInvalidated() => _host.Dispatch(new VisualInvalidated());
}
