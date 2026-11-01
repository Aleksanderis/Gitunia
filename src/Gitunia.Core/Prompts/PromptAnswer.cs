namespace Gitunia.Core.Prompts;

/// <summary>What the user answered: the typed text and, for a picker, the chosen candidate.</summary>
/// <param name="Text">The text in the field when Enter was pressed (the filter of a picker).</param>
/// <param name="Choice">The <see cref="PromptChoice.Value"/> of the highlighted candidate; null for a text prompt.</param>
public sealed record PromptAnswer(string Text, string? Choice);
