using Gitunia.Core.Text;

namespace Gitunia.Core.Visual;

/// <summary>A key, a cursor move or a result of Visual mode (range selection) in the active tab.</summary>
public abstract record VisualCommand : AppCommand;

/// <summary><c>v</c> or a drag: starts a range at the cursor of the focused pane, or ends the active one.</summary>
/// <param name="CursorRow">The cursor row of the focused pane's list, null when it has none.</param>
/// <param name="HasRangeSource">The focused pane's list draws ranges.</param>
public sealed record VisualToggled(int? CursorRow, bool HasRangeSource) : VisualCommand;

/// <summary>The character-wise key: starts a character-wise selection at the cursor, turns a row-wise one into it, or ends it.</summary>
/// <param name="CursorRow">The cursor row of the focused pane's list, null when it has none.</param>
/// <param name="CursorColumn">The column the selection starts at.</param>
/// <param name="HasTextSource">The focused pane shows text lines that can be selected by character.</param>
public sealed record VisualCharsToggled(int? CursorRow, int CursorColumn, bool HasTextSource) : VisualCommand;

/// <summary>A character motion moved the cursor end of a character-wise selection to a column of its row.</summary>
/// <param name="Column">The new column.</param>
public sealed record VisualColumnMoved(int Column) : VisualCommand;

/// <summary>The mouse (a drag, a double or triple click) selected characters in the focused pane.</summary>
/// <param name="Anchor">Where the selection starts.</param>
/// <param name="Cursor">Where its moving end is.</param>
public sealed record VisualCharsSelected(TextPosition Anchor, TextPosition Cursor) : VisualCommand;

/// <summary>The cursor of the active range's pane moved: the far end of the range follows it.</summary>
/// <param name="Row">The new cursor row.</param>
public sealed record VisualCursorMoved(int Row) : VisualCommand;

/// <summary><c>y</c>: copy the rows of the active range.</summary>
public sealed record VisualCopyRequested : VisualCommand;

/// <summary>The terminal layer copied (or failed to copy) the rows of the range.</summary>
/// <param name="Copied">The text reached the clipboard; false keeps the range so the user can retry.</param>
/// <param name="Notice">A notice for the status bar, empty when there is none.</param>
public sealed record VisualCopied(bool Copied, string Notice) : VisualCommand;

/// <summary><c>Esc</c>, <c>q</c>, a click or any other reason to leave Visual mode.</summary>
public sealed record VisualEnded : VisualCommand;

/// <summary>The rows the range refers to changed meaning (new rows, wrap or width changed): the range ends and says so.</summary>
public sealed record VisualInvalidated : VisualCommand;
