using Gitunia.Core.Overlays;
using Gitunia.Core.Text;

namespace Gitunia.Core.Prompts;

/// <summary>Draws an <see cref="OpenPrompt"/> as the shared prompt popup.</summary>
public static class OpenPromptPopup
{
    /// <summary>The popup for an open prompt: a text field, or a field with the candidate list under it.</summary>
    public static PromptPopup Of(OpenPrompt prompt)
    {
        if (prompt.KeyChoices is { } keys)
        {
            return PromptPopup.ForChoice(
                prompt.Title,
                prompt.Context,
                [.. keys.Select(choice => $"{choice.Key}  {choice.Text}")]);
        }

        if (prompt.Picker is not { } picker)
        {
            return PromptPopup.ForText(prompt.Title, prompt.Context, prompt.Text, prompt.AcceptLabel)
                with { InputCaret = prompt.Caret };
        }

        if (prompt.ListOnly)
        {
            return ListPopup(prompt, picker);
        }

        return PromptPopup.ForFilter(
            prompt.Title,
            prompt.Context,
            prompt.Text,
            picker.Rows(),
            prompt.AcceptLabel,
            picker.Candidates.Length > 1) with { InputCaret = prompt.Caret };
    }

    private static PromptPopup ListPopup(OpenPrompt prompt, PromptPicker picker)
    {
        var window = picker.Rows();

        return new PromptPopup(
            prompt.Title,
            [prompt.Context],
            null,
            window.Rows,
            [new("Enter", prompt.AcceptLabel), new("j/k/h/l", HintVerb.Move), new("Esc", HintVerb.Cancel)],
            Highlight: window.Highlight,
            Count: window.Position);
    }
}
