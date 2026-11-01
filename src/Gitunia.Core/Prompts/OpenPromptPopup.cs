using Gitunia.Core.Overlays;

namespace Gitunia.Core.Prompts;

/// <summary>Draws an <see cref="OpenPrompt"/> as the shared prompt popup.</summary>
public static class OpenPromptPopup
{
    /// <summary>The popup for an open prompt: a text field, or a field with the candidate list under it.</summary>
    public static PromptPopup Of(OpenPrompt prompt)
    {
        if (prompt.Picker is not { } picker)
        {
            return PromptPopup.ForText(prompt.Title, prompt.Context, prompt.Text, prompt.AcceptLabel);
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
            picker.Candidates.Length > 1);
    }

    private static PromptPopup ListPopup(OpenPrompt prompt, PromptPicker picker)
    {
        var names = picker.Candidates.Select(choice => choice.Text).ToList();
        var position = PickerRows.Position(picker.Index, names.Count);

        return new PromptPopup(
            prompt.Title,
            [$"{prompt.Context} {position}"],
            null,
            PickerRows.Window(names, picker.Index),
            [new("Enter", prompt.AcceptLabel), new("j/k/h/l", "select"), new("Esc", "cancel")]);
    }
}
