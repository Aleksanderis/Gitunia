namespace Gitunia.Core.Conflicts;

/// <summary>
/// What the editor last copied, kept for <c>p</c> and <c>P</c> (the terminal clipboard can be written but not read,
/// decisions.md, Clipboard #52, so the editor keeps its own copy of the text it put there).
/// </summary>
/// <param name="Text">The text, lines joined with <c>\n</c>.</param>
/// <param name="IsLinewise">True for whole lines (<c>yy</c>, a Visual <c>y</c>, <c>dd</c>): a put opens new lines.</param>
public sealed record ConflictYank(string Text, bool IsLinewise);
