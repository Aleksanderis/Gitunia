using Gitunia.Core.Text;

namespace Gitunia.Core.Editing;

public sealed partial class MessageEditor
{
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
        _anchor = null;
        var row = CursorRow + direction;
        if (row < 0 || row >= _lines.Count)
        {
            return;
        }

        CursorRow = row;
        CursorColumn = ColumnAtWidth(_lines[row], _desiredWidth);
    }

    // The last element boundary whose display column does not pass the wanted one (a wide character is never split).
    private static int ColumnAtWidth(string line, int width)
    {
        var column = 0;
        var used = 0;
        while (column < line.Length)
        {
            var next = NextBoundary(line, column);
            var elementWidth = DisplayWidth.Of(line[column..next]);
            if (used + elementWidth > width)
            {
                break;
            }

            used += elementWidth;
            column = next;
        }

        return column;
    }

    private void MoveTo(int row, int column)
    {
        _anchor = null;
        CursorRow = row;
        CursorColumn = column;
        RememberColumn();
    }

    private void RememberColumn() => _desiredWidth = CursorDisplayColumn;
}
