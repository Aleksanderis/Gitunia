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

        return PromptPopup.ForFilter(
            prompt.Title,
            prompt.Context,
            prompt.Text,
            picker.Rows(),
            prompt.AcceptLabel,
            picker.Candidates.Length > 1);
    }
}
