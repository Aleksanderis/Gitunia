namespace Gitunia.Tui.App.Blame;

/// <summary>A place under the pointer in the blame view.</summary>
/// <param name="Line">The file line.</param>
/// <param name="Column">The index of the character in the line's text; 0 in the gutter.</param>
/// <param name="IsCode">True when the pointer is on the code column, false in the gutter.</param>
public sealed record BlamePoint(int Line, int Column, bool IsCode);
