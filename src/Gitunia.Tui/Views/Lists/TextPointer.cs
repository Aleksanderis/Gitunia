using Gitunia.Core.Text;

namespace Gitunia.Tui.Views.Lists;

/// <summary>A pointer event on the text of a list pane.</summary>
/// <param name="Kind">What the pointer did.</param>
/// <param name="Position">The character under the pointer (clamped to the rows).</param>
/// <param name="Origin">For <see cref="TextPointerKind.DragStart"/>, the character where the button went down.</param>
public readonly record struct TextPointer(TextPointerKind Kind, TextPosition Position, TextPosition? Origin = null);
