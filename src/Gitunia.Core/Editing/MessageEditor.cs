using System.Globalization;
using System.Text;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Editing;

/// <summary>
/// The multi-line commit message editor (Insert mode of the working-tree message box): lines, a cursor and the editing keys.
/// Renderer-agnostic and pure: it never sees the terminal. The cursor always sits between whole text elements, so a wide
/// character, an emoji sequence or a letter with combining marks is never split. Keys that do not edit
/// (<c>Esc</c>, <c>Alt-Enter</c>, <c>Ctrl-g</c>, the <c>Alt-</c> option toggles) are left to the caller.
/// </summary>
public sealed class MessageEditor
{
    /// <summary>The width after which the subject line gets a hint; never enforced.</summary>
    public const int SubjectSoftLimit = 72;

    private const char NewLine = '\n';
    private const char CarriageReturn = '\r';
    private const char Tab = '\t';
    private const string WindowsLineEnding = "\r\n";

    private readonly List<string> _lines = [string.Empty];
    private int _desiredElements;

    /// <summary>The lines of the message; at least one (possibly empty).</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Zero-based line the cursor is on.</summary>
    public int CursorRow { get; private set; }

    /// <summary>The cursor position in the line as a UTF-16 index, always at the start of a text element.</summary>
    public int CursorColumn { get; private set; }

    /// <summary>The message as git takes it: lines joined with <c>\n</c>.</summary>
    public string Text => string.Join(NewLine, _lines);

    /// <summary>True when there is nothing but whitespace (git refuses such a message).</summary>
    public bool IsBlank => _lines.All(string.IsNullOrWhiteSpace);

    /// <summary>Replaces the whole text and puts the cursor at its end.</summary>
    public void SetText(string text)
    {
        _lines.Clear();
        _lines.Add(string.Empty);
        CursorRow = 0;
        CursorColumn = 0;
        Insert(text);
    }

    /// <summary>Empties the editor.</summary>
    public void Clear() => SetText(string.Empty);

    /// <summary>Inserts text at the cursor (typing or a paste): line endings become new lines, other control characters are dropped.</summary>
    public void Insert(string text)
    {
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

    /// <summary>Applies a key; returns false when it is not an editing key (the caller handles it).</summary>
    public bool Apply(KeyEvent rawKey)
    {
        var key = rawKey.Normalize();
        if (key.IsPrintable)
        {
            Insert(key.Name);
            return true;
        }

        return key.Modifiers switch
        {
            KeyModifiers.None => ApplyPlain(key.Name),
            KeyModifiers.Ctrl => ApplyControl(key.Name),
            _ => false,
        };
    }

    private static int NextBoundary(string line, int column) => column + StringInfo.GetNextTextElementLength(line.AsSpan(column));

    private static int PreviousBoundary(string line, int column)
    {
        var previous = 0;
        for (var boundary = 0; boundary < column; boundary = NextBoundary(line, boundary))
        {
            previous = boundary;
        }

        return previous;
    }

    private bool ApplyPlain(string name)
    {
        switch (name)
        {
            case KeyNames.Enter:
                Insert(NewLine.ToString());
                return true;
            case KeyNames.Backspace:
                DeleteBackward();
                return true;
            case KeyNames.Delete:
                DeleteForward();
                return true;
            case KeyNames.Left:
                MoveLeft();
                return true;
            case KeyNames.Right:
                MoveRight();
                return true;
            case KeyNames.Up:
                MoveVertically(-1);
                return true;
            case KeyNames.Down:
                MoveVertically(1);
                return true;
            case KeyNames.Home:
                MoveTo(CursorRow, 0);
                return true;
            case KeyNames.End:
                MoveTo(CursorRow, _lines[CursorRow].Length);
                return true;
            default:
                return false;
        }
    }

    private bool ApplyControl(string name)
    {
        switch (name)
        {
            case "a":
                MoveTo(CursorRow, 0);
                return true;
            case "e":
                MoveTo(CursorRow, _lines[CursorRow].Length);
                return true;
            case "u":
                DeleteTo(0);
                return true;
            case "k":
                DeleteTo(_lines[CursorRow].Length);
                return true;
            case "w":
                DeleteWordBackward();
                return true;
            default:
                return false;
        }
    }

    private void DeleteBackward()
    {
        if (CursorColumn > 0)
        {
            DeleteTo(PreviousBoundary(_lines[CursorRow], CursorColumn));
        }
        else if (CursorRow > 0)
        {
            JoinWithPrevious();
        }
    }

    private void DeleteForward()
    {
        var line = _lines[CursorRow];
        if (CursorColumn < line.Length)
        {
            DeleteTo(NextBoundary(line, CursorColumn));
        }
        else if (CursorRow < _lines.Count - 1)
        {
            _lines[CursorRow] += _lines[CursorRow + 1];
            _lines.RemoveAt(CursorRow + 1);
        }
    }

    private void DeleteWordBackward()
    {
        if (CursorColumn == 0)
        {
            DeleteBackward();
            return;
        }

        var line = _lines[CursorRow];
        var start = CursorColumn;
        while (start > 0 && char.IsWhiteSpace(line[start - 1]))
        {
            start--;
        }

        while (start > 0 && !char.IsWhiteSpace(line[start - 1]))
        {
            start--;
        }

        DeleteTo(start);
    }

    private void JoinWithPrevious()
    {
        var previousLength = _lines[CursorRow - 1].Length;
        _lines[CursorRow - 1] += _lines[CursorRow];
        _lines.RemoveAt(CursorRow);
        CursorRow--;
        CursorColumn = previousLength;
        RememberColumn();
    }

    // Deletes between the cursor and another column of the cursor's line.
    private void DeleteTo(int column)
    {
        var start = Math.Min(column, CursorColumn);
        var end = Math.Max(column, CursorColumn);
        _lines[CursorRow] = _lines[CursorRow].Remove(start, end - start);
        CursorColumn = start;
        RememberColumn();
    }

    private void MoveLeft()
    {
        if (CursorColumn > 0)
        {
            MoveTo(CursorRow, PreviousBoundary(_lines[CursorRow], CursorColumn));
        }
        else if (CursorRow > 0)
        {
            MoveTo(CursorRow - 1, _lines[CursorRow - 1].Length);
        }
    }

    private void MoveRight()
    {
        var line = _lines[CursorRow];
        if (CursorColumn < line.Length)
        {
            MoveTo(CursorRow, NextBoundary(line, CursorColumn));
        }
        else if (CursorRow < _lines.Count - 1)
        {
            MoveTo(CursorRow + 1, 0);
        }
    }

    private void MoveVertically(int direction)
    {
        var row = CursorRow + direction;
        if (row < 0 || row >= _lines.Count)
        {
            return;
        }

        var line = _lines[row];
        var column = 0;
        for (var element = 0; element < _desiredElements && column < line.Length; element++)
        {
            column = NextBoundary(line, column);
        }

        CursorRow = row;
        CursorColumn = column;
    }

    private void MoveTo(int row, int column)
    {
        CursorRow = row;
        CursorColumn = column;
        RememberColumn();
    }

    private void RememberColumn()
    {
        var line = _lines[CursorRow];
        var elements = 0;
        for (var column = 0; column < CursorColumn; column = NextBoundary(line, column))
        {
            elements++;
        }

        _desiredElements = elements;
    }
}
