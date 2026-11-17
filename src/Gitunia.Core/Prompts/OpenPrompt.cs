using System.Collections.Immutable;
using Gitunia.Core.Editing;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Prompts;

/// <summary>An open prompt in <see cref="Shell.ShellState"/>: the texts, the typed text, the picker and the result.</summary>
/// <param name="Title">The popup title.</param>
/// <param name="Context">What the answer is for.</param>
/// <param name="AcceptLabel">What <c>Enter</c> does.</param>
/// <param name="Field">The text typed so far and its caret.</param>
/// <param name="Picker">The candidates when the prompt chooses from a list; null for plain text.</param>
/// <param name="OnAccepted">Turns the answer into the feature's command.</param>
/// <param name="Owner">The tab the prompt belongs to, or null.</param>
/// <param name="ReturnMode">The key mode to restore when the prompt closes.</param>
/// <param name="ListOnly">The picker has no text field and moves on <c>j/k/h/l</c> as well.</param>
/// <param name="KeyChoices">The options when one key answers the prompt; null otherwise.</param>
public sealed record OpenPrompt(
    string Title,
    string Context,
    string AcceptLabel,
    TextField Field,
    PromptPicker? Picker,
    IPromptResult OnAccepted,
    TabId? Owner,
    KeyMode ReturnMode,
    bool ListOnly = false,
    ImmutableArray<PromptKeyChoice>? KeyChoices = null)
{
    /// <summary>The text typed so far.</summary>
    public string Text => Field.Text;
}
