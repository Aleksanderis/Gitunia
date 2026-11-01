using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Prompts;

/// <summary>An open prompt in <see cref="Shell.ShellState"/>: the texts, the typed text, the picker and the result.</summary>
/// <param name="Title">The popup title.</param>
/// <param name="Context">What the answer is for.</param>
/// <param name="AcceptLabel">What <c>Enter</c> does.</param>
/// <param name="Text">The text typed so far. The caret is always at its end: prompts edit at the end.</param>
/// <param name="Picker">The candidates when the prompt chooses from a list; null for plain text.</param>
/// <param name="OnAccepted">Turns the answer into the feature's command.</param>
/// <param name="Owner">The tab the prompt belongs to, or null.</param>
/// <param name="ReturnMode">The key mode to restore when the prompt closes.</param>
/// <param name="ListOnly">The picker has no text field and moves on <c>j/k/h/l</c> as well.</param>
public sealed record OpenPrompt(
    string Title,
    string Context,
    string AcceptLabel,
    string Text,
    PromptPicker? Picker,
    IPromptResult OnAccepted,
    TabId? Owner,
    KeyMode ReturnMode,
    bool ListOnly = false);
