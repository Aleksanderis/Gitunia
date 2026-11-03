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
                SelectChars(_dragAnchor.Value, pointed.Position);
                break;
            case TextPointerKind.DragMove when _dragAnchor is { } anchor:
                SelectChars(anchor, pointed.Position);
                break;
            case TextPointerKind.Click:
                _pointed = null;
                break;
            default:
                _pointed = pointed.Position;
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
            var text = source.TextOf(selection.Range.Cursor);
            var column = VisualActions.MoveColumn(action, text, selection.CursorColumn, count ?? 1);
            _host.Dispatch(new VisualColumnMoved(column));
        }

        return IsActive;
    }

    private void ToggleChars()
    {
        var pane = _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph;
        var list = _lists.ListOf(pane);
        var hasText = TextOfPane(pane) is not null;

        _host.Dispatch(new VisualCharsToggled(list.SelectedItem, 0, hasText));
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

        var current = _host.State.Shell.Visual is { Kind: VisualKind.Chars } selection
            ? new TextPosition(selection.Range.Cursor, selection.CursorColumn)
            : new TextPosition(row, 0);
        var at = _pointed ?? current;
        _pointed = null;
        var text = source.TextOf(at.Line);
        var span = isWord ? TextMotions.WordAt(text, at.Column) : new ColumnSpan(0, TextMotions.LineEnd(text));
        SelectChars(new TextPosition(at.Line, span.First), new TextPosition(at.Line, span.Last));
    }

    private void SelectChars(TextPosition anchor, TextPosition cursor) =>
        _host.Dispatch(new VisualCharsSelected(anchor, cursor));

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

        if (_source is not null && (_source.Range != selection.Range || (_source as ITextSource)?.Text != selection.Text))
        {
            _source.Range = selection.Range;
            SetText(selection.Text);
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
        SetText(null);
        _source = null;
        _lists.ListOf(_pane).SetNeedsDraw();
    }

    private void SetText(TextSelection? text)
    {
        if (_source is ITextSource source)
        {
            source.Text = text;
        }
    }

    private void OnInvalidated() => _host.Dispatch(new VisualInvalidated());
}
