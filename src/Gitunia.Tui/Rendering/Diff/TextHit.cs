using Gitunia.Core.Text;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>A character under the pointer and the side of the side-by-side view it is on.</summary>
/// <param name="Position">The screen line and the index of the character in the text of the row the line shows.</param>
/// <param name="IsOldSide">The left (old) side of the side-by-side view; false elsewhere.</param>
public readonly record struct TextHit(TextPosition Position, bool IsOldSide);
