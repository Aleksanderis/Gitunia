using Gitunia.Core.Overlays;
using Gitunia.Tui.Input;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// Keeps the prompt popup in step with the prompts: asks for a redraw when the popup opens, closes, changes its text
/// or, while it or any other overlay is open, when the screen size changed since it was last drawn (a resize places it
/// again).
/// </summary>
/// <param name="prompts">The prompts; the popup of the open one is the current popup.</param>
/// <param name="chrome">The screen size.</param>
public sealed class PromptPopupTracker(PromptDisplays prompts, IPaneChrome chrome)
{
    private PromptPopup? _shown;
    private ScreenSize _shownSize;
    private bool _overlayShown;

    /// <summary>Raised when the overlay frame is to be built and shown again; the shower calls <see cref="MarkShown"/>.</summary>
    public event Action? RedrawRequested;

    /// <summary>Records what the overlay layer was just drawn with.</summary>
    /// <param name="overlayShown">True when the layer shows a frame (help, palette, dialog or popup).</param>
    public void MarkShown(bool overlayShown)
    {
        _overlayShown = overlayShown;
        _shown = prompts.CurrentPopup();
        _shownSize = chrome.ScreenSize;
    }

    /// <summary>Redraws when the popup or the screen size differs from what was last drawn.</summary>
    public void Sync()
    {
        var popup = prompts.CurrentPopup();
        var same = popup is null ? _shown is null : popup.SameText(_shown);
        if (!same || ((popup is not null || _overlayShown) && _shownSize != chrome.ScreenSize))
        {
            RedrawRequested?.Invoke();
        }
    }
}
