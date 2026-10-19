namespace Gitunia.Core.Config.Models;

/// <summary>The outcome of a pure text edit of the config.</summary>
/// <param name="Edited">False when the edit was refused; the text is then the unchanged input.</param>
/// <param name="Text">The new text.</param>
/// <param name="Message">Why the edit was refused (empty when edited).</param>
/// <param name="Shadowed">True when the key is written more than once and the last one was changed.</param>
public sealed record ConfigEditResult(bool Edited, string Text, string Message, bool Shadowed);
