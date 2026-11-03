namespace Gitunia.Core.Editing;

public sealed partial class MessageEditor
{
    private readonly List<RedoStep> _redo = [];
    private TextPosition? _anchor;

    /// <summary>The selected text, from the anchor to the cursor; null when nothing is selected.</summary>
    public TextSelection? Selection
    {
        get
        {
            if (_anchor is not { } anchor)
            {
                return null;
            }

            var cursor = new TextPosition(CursorRow, CursorColumn);
            if (anchor == cursor)
            {
                return null;
            }

            return IsBefore(anchor, cursor) ? new TextSelection(anchor, cursor) : new TextSelection(cursor, anchor);
        }
    }

    /// <summary>True while some text is selected.</summary>
    public bool HasSelection => Selection is not null;

    /// <summary>The selected text with lines joined by <c>\n</c>; empty when nothing is selected.</summary>
    public string SelectedText
    {
        get
        {
            if (Selection is not { } selection)
            {
                return string.Empty;
            }

            if (selection.Start.Row == selection.End.Row)
            {
                var line = _lines[selection.Start.Row];

                return line[selection.Start.Column..selection.End.Column];
            }

            var parts = new List<string> { _lines[selection.Start.Row][selection.Start.Column..] };
            for (var row = selection.Start.Row + 1; row < selection.End.Row; row++)
            {
                parts.Add(_lines[row]);
            }

            parts.Add(_lines[selection.End.Row][..selection.End.Column]);

            return string.Join(NewLine, parts);
        }
    }

    /// <summary>Drops the selection and leaves the cursor where it is.</summary>
    public void ClearSelection() => _anchor = null;

    /// <summary>Deletes the selected text and puts the cursor where it started; false when nothing is selected.</summary>
    public bool DeleteSelection()
    {
        if (Selection is not { } selection)
        {
            _anchor = null;

            return false;
        }

        var start = selection.Start;
        var end = selection.End;
        _lines[start.Row] = $"{_lines[start.Row][..start.Column]}{_lines[end.Row][end.Column..]}";
        _lines.RemoveRange(start.Row + 1, end.Row - start.Row);
        MoveTo(start.Row, start.Column);

        return true;
    }

    /// <summary>
    /// Extends the selection to where the pointer is (a drag): the selection starts at the cursor when none exists.
    /// </summary>
    /// <param name="row">Zero-based line, clamped to the text.</param>
    /// <param name="displayColumn">Display column in that line.</param>
    public void ExtendTo(int row, int displayColumn) => ExtendWith(() => PlaceCursor(row, displayColumn));

    /// <summary>Selects the run of word characters, symbols or whitespace at the cursor (a double-click).</summary>
    public void SelectWordAtCursor()
    {
        var range = WordBoundaries.Around(_lines[CursorRow], CursorColumn);
        MoveTo(CursorRow, range.Start);
        ExtendWith(() => MoveTo(CursorRow, range.End));
    }

    /// <summary>Returns to the text before the last <see cref="Undo"/>; false when there is nothing to redo.</summary>
    public bool Redo()
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        var step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        if (!step.Expected.Lines.SequenceEqual(_lines))
        {
            _redo.Clear();

            return false;
        }

        _undo.Add(step.Expected);
        Restore(step.Target);

        return true;
    }

    // Runs a cursor move that keeps or starts the selection at the cursor's old place.
    private void ExtendWith(Action move)
    {
        var anchor = _anchor ?? new TextPosition(CursorRow, CursorColumn);
        move();
        _anchor = anchor;
    }

    private void Restore(EditorSnapshot snapshot)
    {
        _lines.Clear();
        _lines.AddRange(snapshot.Lines);
        MoveTo(snapshot.Row, snapshot.Column);
    }

    private static bool IsBefore(TextPosition first, TextPosition second) =>
        first.Row < second.Row || (first.Row == second.Row && first.Column < second.Column);
}
