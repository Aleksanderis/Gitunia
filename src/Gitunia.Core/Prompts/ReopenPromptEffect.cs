namespace Gitunia.Core.Prompts;

/// <summary>
/// A feature rejected the answer of the text prompt that just closed (an empty or taken name, an unchanged value): the
/// runner turns it into <see cref="PromptReopenedCommand"/>, which opens the prompt again with what was typed and posts
/// <paramref name="Reason"/> as a warning. The feature builds no prompt of its own, so the typed text is never lost
/// (docs/internal/ui-consistency.md, decision 5).
/// </summary>
/// <param name="Reason">Why the answer was rejected; it is also the status line.</param>
public sealed record ReopenPromptEffect(string Reason) : AppEffect;
