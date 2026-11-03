using System.Globalization;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Editing;

public sealed partial class MessageEditor
{
    /// <summary>
    /// Applies a key; returns false when it is not an editing key (the caller handles it). Shift with a movement key
    /// extends the selection; typing or a deleting key replaces or deletes it; <c>Left</c> and <c>Right</c> collapse it
    /// to its start or end.
    /// </summary>
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

        if (key.Modifiers.HasFlag(KeyModifiers.Shift) && IsMovement(key))
        {
            var moved = key with { Modifiers = key.Modifiers & ~KeyModifiers.Shift };
            ExtendWith(() => ApplyKey(moved));

            return true;
        }

        if (HasSelection && DeletesText(key))
        {
            DeleteSelection();

            return true;
        }

        if (key.Modifiers == KeyModifiers.None && key.Name is KeyNames.Left or KeyNames.Right && Selection is { } range)
        {
            var edge = key.Name == KeyNames.Left ? range.Start : range.End;
            MoveTo(edge.Row, edge.Column);

            return true;
        }

        return ApplyKey(key);
    }

    private bool ApplyKey(KeyEvent key)
    {
        return key.Modifiers switch
        {
            KeyModifiers.None or KeyModifiers.Shift => ApplyPlain(key.Name),
            KeyModifiers.Ctrl => ApplyControl(key.Name),
            KeyModifiers.Alt => ApplyAlt(key.Name),
            _ => false,
        };
    }

    // Keys that move the cursor; with Shift they extend the selection (Ctrl and Alt make them word or text steps).
    private static bool IsMovement(KeyEvent key)
    {
        var modifiers = key.Modifiers & ~KeyModifiers.Shift;
        if (modifiers == KeyModifiers.Alt)
        {
            return key.Name is KeyNames.Left or KeyNames.Right or "b" or "f";
        }

        if (modifiers == KeyModifiers.Ctrl)
        {
            return key.Name is KeyNames.Left or KeyNames.Right or KeyNames.Home or KeyNames.End;
        }

        return modifiers == KeyModifiers.None
            && key.Name is KeyNames.Left or KeyNames.Right or KeyNames.Up or KeyNames.Down or KeyNames.Home
                or KeyNames.End;
    }

    // Keys that delete before or after the cursor; with a selection they delete the selection and nothing else.
    private static bool DeletesText(KeyEvent key) => key.Modifiers switch
    {
        KeyModifiers.None or KeyModifiers.Shift => key.Name is KeyNames.Backspace or KeyNames.Delete,
        KeyModifiers.Ctrl => key.Name is KeyNames.Backspace or KeyNames.Delete or "w" or "u" or "k",
        KeyModifiers.Alt => key.Name is KeyNames.Backspace or "d",
        _ => false,
    };

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
                DeleteSelection();
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
            case KeyNames.Backspace:
                DeleteWordBackward();
                return true;
            case KeyNames.Delete:
                DeleteWordForward();
                return true;
            case KeyNames.Left:
                MoveWordLeft();
                return true;
            case KeyNames.Right:
                MoveWordRight();
                return true;
            case KeyNames.Home:
                MoveTo(0, 0);
                return true;
            case KeyNames.End:
                MoveTo(_lines.Count - 1, _lines[^1].Length);
                return true;
            default:
                return false;
        }
    }

    // Alt-arrows too: many terminals report Ctrl-arrows as Alt-arrows (docs/internal/ux-workflows.md, message box).
    private bool ApplyAlt(string name)
    {
        switch (name)
        {
            case "b":
            case KeyNames.Left:
                MoveWordLeft();
                return true;
            case "f":
            case KeyNames.Right:
                MoveWordRight();
                return true;
            case "d":
                DeleteWordForward();
                return true;
            case KeyNames.Backspace:
                DeleteWordBackward();
                return true;
            default:
                return false;
        }
    }

    // Word steps wrap to the neighbouring line at the line edges, like Left and Right.
    private void MoveWordLeft()
    {
        if (CursorColumn > 0)
        {
            MoveTo(CursorRow, WordBoundaries.Previous(_lines[CursorRow], CursorColumn));
        }
        else
        {
            MoveLeft();
        }
    }

    private void MoveWordRight()
    {
        var line = _lines[CursorRow];
        if (CursorColumn < line.Length)
        {
            MoveTo(CursorRow, WordBoundaries.Next(line, CursorColumn));
        }
        else
        {
            MoveRight();
        }
    }

    private void DeleteWordForward()
    {
        var line = _lines[CursorRow];
        if (CursorColumn < line.Length)
        {
            DeleteTo(WordBoundaries.Next(line, CursorColumn));
        }
        else
        {
            DeleteForward();
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

        DeleteTo(WordBoundaries.Previous(_lines[CursorRow], CursorColumn));
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
