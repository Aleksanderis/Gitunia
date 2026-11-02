using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Pick;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Overlays;

/// <summary>The frames of the prompt popup and the mainline question, which show only when no other overlay is open.</summary>
/// <param name="pick">The mainline question.</param>
/// <param name="prompts">The text and choice prompts.</param>
/// <param name="chrome">The screen size.</param>
/// <param name="config">The glyphs the frames are drawn with.</param>
/// <param name="cover">Which overlays are open.</param>
public sealed class PopupFrames(
    PickActions pick,
    PromptDisplays prompts,
    IPaneChrome chrome,
    LiveConfig config,
    OverlayCover cover)
{
    /// <summary>The popup of the open text or choice prompt, null when none is open.</summary>
    public OverlayFrame? PopupFrame()
    {
        if (prompts.CurrentPopup() is not { } popup)
        {
            return null;
        }

        var size = chrome.ScreenSize;

        return PromptPopupFrameBuilder.Build(popup, size.Width, size.Height, config.Glyphs);
    }

    /// <summary>The mainline question of a cherry-pick or revert of a merge commit, null when none is open.</summary>
    public OverlayFrame? MainlineFrame()
    {
        if (pick.Prompt is not { } prompt)
        {
            return null;
        }

        var size = chrome.ScreenSize;

        return MainlineFrameBuilder.Build(
            prompt.Title,
            prompt.Parents,
            size.Width,
            size.Height,
            config.Glyphs,
            prompt.UsableParents);
    }

    /// <summary>The status bar carries the prompt only when the screen is too small for its popup.</summary>
    public string PopupFallbackLine()
    {
        if (prompts.CurrentPopup() is not { } popup || cover.IsMainlineCovered() || PopupFrame() is not null)
        {
            return string.Empty;
        }

        return popup.Input is { } input ? $"{popup.Title}: {input}_  {popup.Hint}" : $"{popup.Title}  {popup.Hint}";
    }
}
