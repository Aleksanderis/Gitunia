using Gitunia.Core.Prompts;

namespace Gitunia.Core.Effects;

/// <summary>Open a prompt; the runner turns it into <see cref="PromptRequestedCommand"/>, like a confirm dialog.</summary>
/// <param name="Request">What to ask and what the answer becomes.</param>
public sealed record ShowPromptEffect(PromptRequest Request) : AppEffect;
