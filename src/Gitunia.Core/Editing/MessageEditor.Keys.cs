using System.Globalization;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Editing;

public sealed partial class MessageEditor
{
    /// <summary>Applies a key; returns false when it is not an editing key (the caller handles it).</summary>
    public bool Apply(KeyEvent rawKey)
    {
        var key = rawKey.Normalize();
        if (key.IsPrintable)
        {
            Insert(key.Name);
            return true;
        }

        if (key.Name == KeyNames.Tab && key.Modifiers != KeyModifiers.None)
        {
            return false;
        }

        return key.Modifiers switch
        {
            KeyModifiers.None or KeyModifiers.Shift => ApplyPlain(key.Name),
            KeyModifiers.Ctrl => ApplyControl(key.Name),
            _ => false,
        };
    }

    private static int NextBoundary(string line, int column) =>
        column + StringInfo.GetNextTextElementLength(line.AsSpan(column));

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
            case KeyNames.Tab:
                Insert(UsesTabCharacters ? Tab.ToString() : new string(' ', TabWidth - CursorDisplayColumn % TabWidth));
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
}
