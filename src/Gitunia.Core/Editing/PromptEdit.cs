namespace Gitunia.Core.Editing;

/// <summary>A single-line prompt's text with its caret, a UTF-16 column on a text element boundary.</summary>
public readonly record struct PromptEdit(string Text, int Caret);
