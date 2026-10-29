namespace Gitunia.Core.Editing;

/// <summary>The text and cursor of a <see cref="MessageEditor"/> at an undo point.</summary>
/// <param name="Lines">The lines.</param>
/// <param name="Row">The cursor line.</param>
/// <param name="Column">The cursor column in the line.</param>
public sealed record EditorSnapshot(string[] Lines, int Row, int Column);
