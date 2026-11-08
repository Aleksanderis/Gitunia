using System.Text;
using Gitunia.Core.Text;

namespace Gitunia.Core.Editing;

/// <summary>
/// The multi-line commit message editor (Insert mode of the working-tree message box): lines, a cursor and the editing
/// keys. Up and Down keep the display column (wide characters take two), <c>Tab</c> inserts spaces up to the next
/// multiple of four columns, and a click places the cursor at a display column. Renderer-agnostic and pure: it never sees
/// the terminal. The cursor always sits between whole text elements, so a wide character, an emoji sequence or a letter with combining marks is never split. Keys that do not edit
/// (<c>Esc</c>, <c>Ctrl-g</c>, the <c>Alt-</c> option toggles) are left to the caller.
/// </summary>
public sealed partial class MessageEditor
{
    /// <summary>The width after which the subject line gets a hint; never enforced.</summary>
    public const int SubjectSoftLimit = 72;

    private const char NewLine = '\n';
    private const char CarriageReturn = '\r';
    private const char Tab = '\t';
    private const int TabWidth = 4;
    private const string WindowsLineEnding = "\r\n";

    private const int MaximumUndo = 100;

    private readonly List<string> _lines = [string.Empty];
    private readonly List<EditorSnapshot> _undo = [];
    private int _desiredWidth;

    /// <summary>
    /// True when <c>Tab</c> inserts a tab character instead of spaces up to the next multiple of four columns (text
    /// where tabs matter, such as a file being resolved).
    /// </summary>
    public bool UsesTabCharacters { get; init; }

    /// <summary>The lines of the message; at least one (possibly empty).</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Zero-based line the cursor is on.</summary>
    public int CursorRow { get; private set; }

    /// <summary>The cursor position in the line as a UTF-16 index, always at the start of a text element.</summary>
    public int CursorColumn { get; private set; }

    /// <summary>The cursor's display column in its line (wide characters take two, combining marks none).</summary>
    public int CursorDisplayColumn => DisplayWidth.Of(_lines[CursorRow][..CursorColumn]);

    /// <summary>Display columns of the first line, the subject.</summary>
    public int SubjectWidth => DisplayWidth.Of(_lines[0]);

    /// <summary>The message as git takes it: lines joined with <c>\n</c>.</summary>
    public string Text => string.Join(NewLine, _lines);

    /// <summary>True when there is nothing but whitespace (git refuses such a message).</summary>
    public bool IsBlank => _lines.All(string.IsNullOrWhiteSpace);

    /// <summary>A separate editor with the same text, cursor, remembered column and undo points.</summary>
    public MessageEditor Clone()
    {
        var copy = new MessageEditor { UsesTabCharacters = UsesTabCharacters };
        copy._lines.Clear();
        copy._lines.AddRange(_lines);
        copy._undo.AddRange(_undo);
        copy._redo.AddRange(_redo);
        copy._anchor = _anchor;
        copy.CursorRow = CursorRow;
        copy.CursorColumn = CursorColumn;
        copy._desiredWidth = _desiredWidth;

        return copy;
    }

    /// <summary>Replaces the whole text and puts the cursor at its end. The undo history is dropped.</summary>
    public void SetText(string text)
    {
        _undo.Clear();
        _redo.Clear();
        _anchor = null;
        _lines.Clear();
        _lines.Add(string.Empty);
        CursorRow = 0;
        CursorColumn = 0;
        Insert(text);
    }

    /// <summary>
    /// Replaces the whole text with <paramref name="lines"/> as they are (no control characters are dropped) and puts
    /// the cursor at the start. The undo history is dropped. An empty list becomes one empty line.
    /// </summary>
    public void LoadLines(IReadOnlyList<string> lines)
    {
        _undo.Clear();
        _redo.Clear();
        _lines.Clear();
        _lines.AddRange(lines);
        if (_lines.Count == 0)
        {
            _lines.Add(string.Empty);
        }

        MoveTo(0, 0);
    }

    /// <summary>
    /// Puts the cursor on a line and a UTF-16 column, both clamped (the column to a text element boundary).
    /// </summary>
    /// <param name="row">Zero-based line.</param>
    /// <param name="column">UTF-16 index in the line.</param>
    public void SetCursor(int row, int column)
    {
        var line = Math.Clamp(row, 0, _lines.Count - 1);
        var text = _lines[line];
        var target = Math.Clamp(column, 0, text.Length);
        var boundary = 0;
        while (boundary < target)
        {
            var next = NextBoundary(text, boundary);
            if (next > target)
            {
                break;
            }

            boundary = next;
        }

        MoveTo(line, boundary);
    }

    /// <summary>
    /// Replaces <paramref name="count"/> lines from <paramref name="start"/> with <paramref name="replacement"/>. A
    /// cursor inside the replaced lines goes to the start of the replacement, one after them moves with the text.
    /// </summary>
    /// <param name="start">Zero-based first line to replace.</param>
    /// <param name="count">Lines to replace; zero inserts.</param>
    /// <param name="replacement">The new lines; empty removes the old ones.</param>
    public void ReplaceLines(int start, int count, IReadOnlyList<string> replacement)
    {
        start = Math.Clamp(start, 0, _lines.Count);
        count = Math.Clamp(count, 0, _lines.Count - start);
        var row = CursorRow;
        var column = CursorColumn;
        _lines.RemoveRange(start, count);
        _lines.InsertRange(start, replacement);
        if (_lines.Count == 0)
        {
            _lines.Add(string.Empty);
        }

        if (row >= start + count)
        {
            row += replacement.Count - count;
        }
        else if (row >= start)
        {
            row = start;
            column = 0;
        }

        SetCursor(row, column);
    }

    /// <summary>
    /// Puts the cursor where a click landed: the line is clamped to the text and the column is a display column in
    /// that line; the cursor goes to the element boundary at or before it, or to the line end past the text.
    /// </summary>
    /// <param name="row">Zero-based line.</param>
    /// <param name="displayColumn">Display column in the line.</param>
    public void PlaceCursor(int row, int displayColumn)
    {
        var line = Math.Clamp(row, 0, _lines.Count - 1);
        MoveTo(line, ColumnAtWidth(_lines[line], Math.Max(0, displayColumn)));
    }

    /// <summary>Empties the editor.</summary>
    public void Clear() => SetText(string.Empty);

    /// <summary>
    /// Inserts text at the cursor (typing or a paste): line endings become new lines, other controls are dropped. A
    /// selection is replaced.
    /// </summary>
    public void Insert(string text)
    {
        DeleteSelection();
        var normalized = text.Replace(WindowsLineEnding, NewLine.ToString()).Replace(CarriageReturn, NewLine);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (character is NewLine or Tab || !char.IsControl(character))
            {
                builder.Append(character);
            }
        }

        var parts = builder.ToString().Split(NewLine);
        var line = _lines[CursorRow];
        var tail = line[CursorColumn..];
        _lines[CursorRow] = $"{line[..CursorColumn]}{parts[0]}";
        for (var index = 1; index < parts.Length; index++)
        {
            CursorRow++;
            _lines.Insert(CursorRow, parts[index]);
        }

        CursorColumn = _lines[CursorRow].Length;
        _lines[CursorRow] += tail;
        RememberColumn();
    }

    /// <summary>Remembers the text and cursor so <see cref="Undo"/> can return to them (the start of a change).</summary>
    public void MarkUndoPoint()
    {
        _redo.Clear();
        _undo.Add(new EditorSnapshot([.. _lines], CursorRow, CursorColumn));
        if (_undo.Count > MaximumUndo)
        {
            _undo.RemoveAt(0);
        }
    }

    /// <summary>Forgets the last undo point when the text still equals it (a change that changed nothing, e.g. <c>i</c> then <c>Esc</c>).</summary>
    public void DropUndoPointIfUnchanged()
    {
        if (_undo.Count > 0 && _undo[^1].Lines.SequenceEqual(_lines))
        {
            _undo.RemoveAt(_undo.Count - 1);
        }
    }

    /// <summary>Returns to the last undo point; false when there is none.</summary>
    public bool Undo()
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        var snapshot = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(new RedoStep(new EditorSnapshot([.. _lines], CursorRow, CursorColumn), snapshot));
        Restore(snapshot);
        return true;
    }

    /// <summary>Normal mode <c>h</c>: one element left, stopping at the line start.</summary>
    public void MoveLeftInLine()
    {
        if (CursorColumn > 0)
        {
            MoveTo(CursorRow, PreviousBoundary(_lines[CursorRow], CursorColumn));
        }
    }

    /// <summary>Normal mode <c>l</c>: one element right, stopping at the line end.</summary>
    public void MoveRightInLine()
    {
        var line = _lines[CursorRow];
        if (CursorColumn < line.Length)
        {
            MoveTo(CursorRow, NextBoundary(line, CursorColumn));
        }
    }

    /// <summary>Normal mode <c>j</c> and <c>k</c>: a line down (1) or up (-1), keeping the display column.</summary>
    /// <param name="direction">1 for down, -1 for up.</param>
    public void MoveLines(int direction) => MoveVertically(direction);

    /// <summary>Normal mode <c>0</c>.</summary>
    public void MoveToLineStart() => MoveTo(CursorRow, 0);

    /// <summary>Normal mode <c>$</c>.</summary>
    public void MoveToLineEnd() => MoveTo(CursorRow, _lines[CursorRow].Length);

    /// <summary>Normal mode <c>w</c>: the start of the next word, across lines; the end of the text when there is none.</summary>
    public void MoveWordForward()
    {
        var row = CursorRow;
        var column = CursorColumn;
        var line = _lines[row];
        while (column < line.Length && !char.IsWhiteSpace(line[column]))
        {
            column++;
        }

        while (true)
        {
            while (column < line.Length && char.IsWhiteSpace(line[column]))
            {
                column++;
            }

            if (column < line.Length)
            {
                MoveTo(row, column);
                return;
            }

            if (row == _lines.Count - 1)
            {
                MoveTo(row, line.Length);
                return;
            }

            row++;
            column = 0;
            line = _lines[row];
        }
    }

    /// <summary>Normal mode <c>b</c>: the start of the word before the cursor, across lines.</summary>
    public void MoveWordBackward()
    {
        var row = CursorRow;
        var column = CursorColumn;
        while (true)
        {
            var line = _lines[row];
            while (column > 0 && char.IsWhiteSpace(line[column - 1]))
            {
                column--;
            }

            if (column > 0)
            {
                while (column > 0 && !char.IsWhiteSpace(line[column - 1]))
                {
                    column--;
                }

                MoveTo(row, column);
                return;
            }

            if (row == 0)
            {
                MoveTo(0, 0);
                return;
            }

            row--;
            column = _lines[row].Length;
        }
    }

    /// <summary>Normal mode <c>x</c>: deletes the element under the cursor (not the line break).</summary>
    public void DeleteUnderCursor()
    {
        var line = _lines[CursorRow];
        if (CursorColumn < line.Length)
        {
            DeleteTo(NextBoundary(line, CursorColumn));
        }
    }

    /// <summary>Normal mode <c>dd</c>: deletes the cursor's line; the last remaining line is emptied.</summary>
    public void DeleteLine()
    {
        if (_lines.Count == 1)
        {
            _lines[0] = string.Empty;
            MoveTo(0, 0);
            return;
        }

        _lines.RemoveAt(CursorRow);
        var row = Math.Min(CursorRow, _lines.Count - 1);
        MoveTo(row, Math.Min(ColumnAtWidth(_lines[row], _desiredWidth), _lines[row].Length));
    }

    /// <summary>Normal mode <c>o</c>: opens an empty line below the cursor's and puts the cursor on it.</summary>
    public void OpenLineBelow()
    {
        _lines.Insert(CursorRow + 1, string.Empty);
        MoveTo(CursorRow + 1, 0);
    }
}
