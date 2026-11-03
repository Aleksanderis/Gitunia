using Gitunia.Core.Diff;
using Gitunia.Core.Visual;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>The rows of a diff as the text lines of one side: what a character-wise selection copies (notes, warnings and the other side's rows are left out).</summary>
/// <param name="document">The diff.</param>
/// <param name="geometry">Tells which rows belong to a side.</param>
/// <param name="isOldSide">The left (old) side of the side-by-side view.</param>
public sealed class DiffRowLines(DiffDocument document, DiffTextGeometry geometry, bool isOldSide) : ITextLines
{
    public int Count => document.Rows.Count;

    public string TextOf(int line) => geometry.TextOf(line);

    public bool IsSkipped(int line) =>
        document.Rows[line].Kind is DiffRowKind.Note or DiffRowKind.Warning || !geometry.IsOnSide(line, isOldSide);
}
