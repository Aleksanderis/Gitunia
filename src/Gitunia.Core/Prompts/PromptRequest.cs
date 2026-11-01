using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Prompts;

/// <summary>
/// Everything a feature decides when it opens a prompt: the texts, the starting text, the candidates of a picker, which
/// tab the prompt belongs to and the result that follows Enter. Opened by <see cref="PromptRequestedCommand"/>.
/// </summary>
/// <param name="Title">The popup title, such as <c>Rename tab</c>.</param>
/// <param name="Context">What the answer is for.</param>
/// <param name="AcceptLabel">What <c>Enter</c> does, such as <c>save</c>.</param>
/// <param name="OnAccepted">Turns the answer into the feature's command.</param>
/// <param name="InitialText">The text the field starts with.</param>
/// <param name="Choices">The candidates of a type-to-filter picker; null for a prompt that only asks for text.</param>
/// <param name="NoMatch">The row (and the notice on Enter) when nothing matches; used by pickers.</param>
/// <param name="Owner">The tab the prompt belongs to; the prompt closes when that tab is no longer the active one.</param>
/// <param name="KeyChoices">The options of a prompt answered by one key; null for a text or picker prompt. Any other key
/// is ignored and Enter does nothing; Esc cancels.</param>
public sealed record PromptRequest(
    string Title,
    string Context,
    string AcceptLabel,
    IPromptResult OnAccepted,
    string InitialText = "",
    ImmutableArray<PromptChoice>? Choices = null,
    string NoMatch = "No match",
    TabId? Owner = null,
    ImmutableArray<PromptKeyChoice>? KeyChoices = null);
