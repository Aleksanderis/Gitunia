namespace Gitunia.Core.Overlays;

/// <summary>One key of a prompt footer and what it does; the renderer draws the key in the key style.</summary>
/// <param name="Keys">The key text, such as <c>Enter</c> or <c>Ctrl-j/k</c>; empty for a plain note.</param>
/// <param name="Label">What the key does, such as <c>create</c>.</param>
public readonly record struct PromptHint(string Keys, string Label)
{
    /// <summary>The text as written in a status line: the key, a space, the label.</summary>
    public string Text => Keys.Length == 0 ? Label : $"{Keys} {Label}";
}
