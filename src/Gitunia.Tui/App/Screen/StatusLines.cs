using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Screen;

/// <summary>The extra lines of the status bar: the key hints, the prompt that has no popup and the repeat mode.</summary>
/// <param name="Hints">The key hints.</param>
/// <param name="Prompts">The prompts, whose status line shows while they are open.</param>
/// <param name="Popups">The prompt shown on the status bar when the screen is too small for its popup.</param>
/// <param name="Repeat">The sticky repeat mode.</param>
public sealed record StatusLines(StatusHints Hints, PromptDisplays Prompts, PopupFrames Popups, RepeatController Repeat)
{
    /// <summary>The line of the open prompt, else the popup's fallback.</summary>
    public string SearchLine()
    {
        var line = Prompts.StatusLine();

        return line.Length > 0 ? line : Popups.PopupFallbackLine();
    }

    /// <summary>The key hints to show now.</summary>
    public IReadOnlyList<KeyHint> CurrentHints() => Hints.Current();
}
