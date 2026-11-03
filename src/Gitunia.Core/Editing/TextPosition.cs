namespace Gitunia.Core.Editing;

/// <summary>A place in the text of a <see cref="MessageEditor"/>.</summary>
/// <param name="Row">Zero-based line.</param>
/// <param name="Column">UTF-16 index in the line, at the start of a text element.</param>
public readonly record struct TextPosition(int Row, int Column);
