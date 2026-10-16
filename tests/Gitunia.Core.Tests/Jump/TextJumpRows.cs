using Gitunia.Core.Jump;

namespace Gitunia.Core.Tests.Jump;

/// <summary>Plain strings as quick-jump rows, every row with the same number of columns.</summary>
public sealed class TextJumpRows(IReadOnlyList<string> texts, int columns = 80, int ellipsisWidth = 1) : IJumpRows
{
    public int Count => texts.Count;

    public int EllipsisWidth => ellipsisWidth;

    public string TextOf(int row) => texts[row];

    public int ColumnsOf(int row) => columns;
}
