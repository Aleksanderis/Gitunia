namespace Gitunia.Core.Prompts;

/// <summary>One candidate of a picker prompt: the text the user sees and filters on, and the value the answer carries.</summary>
/// <param name="Value">What the answer carries when this candidate is chosen (a remote name, a branch name).</param>
/// <param name="Text">How the candidate is written in the list.</param>
/// <param name="Match">What the typed text is matched against; null matches <paramref name="Text"/>.</param>
public sealed record PromptChoice(string Value, string Text, string? Match = null);
