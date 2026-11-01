namespace Gitunia.Core.Prompts;

/// <summary>One option of a prompt that is answered by a single key (no typing, no Enter).</summary>
/// <param name="Key">The key that picks it, such as <c>s</c>.</param>
/// <param name="Text">How the option is described next to its key.</param>
/// <param name="Value">What the answer carries when the key is pressed.</param>
public sealed record PromptKeyChoice(string Key, string Text, string Value);
