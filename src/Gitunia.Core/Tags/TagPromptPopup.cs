using Gitunia.Core.Overlays;

namespace Gitunia.Core.Tags;

/// <summary>Draws a <see cref="TagPrompt"/> as the shared prompt popup.</summary>
public static class TagPromptPopup
{
    /// <summary>The popup for an open prompt.</summary>
    public static PromptPopup Of(TagPrompt prompt) => prompt.Kind switch
    {
        TagPromptKind.CreateName => PromptPopup.ForText("New tag", $"At {prompt.Label}", prompt.Text, "next"),
        TagPromptKind.CreateMessage => PromptPopup.ForText(
            "Tag message",
            $"{prompt.NewName}: empty makes a lightweight tag",
            prompt.Text,
            "create"),
        TagPromptKind.PushRemote => RemotePopup(prompt, "Push tag", $"Remote to push {prompt.TagName} to"),
        _ => RemotePopup(prompt, "Delete tag on remote", $"Remote to delete {prompt.TagName} from"),
    };

    private static PromptPopup RemotePopup(TagPrompt prompt, string title, string context)
    {
        var picker = new FilterPicker<string>();
        picker.Reset(prompt.Candidates);
        picker.Move(prompt.Index);
        return picker.ToPopup(title, context, prompt.Text, "choose", remote => remote, "No matching remote");
    }
}
