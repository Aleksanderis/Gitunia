using Gitunia.Core.Overlays;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// Keeps the prompt popup in step with the prompts: asks for a redraw when the popup opens, closes, changes its text
/// or when the screen size changed since it was last drawn (a resize places it again).
/// </summary>
/// <param name="current">The popup of the open prompt, null when none is open.</param>
/// <param name="size">The screen size.</param>
/// <param name="show">Builds and shows the overlay frame; it must call <see cref="MarkShown"/>.</param>
public sealed class PromptPopupTracker(Func<PromptPopup?> current, Func<ScreenSize> size, Action show)
{
    private PromptPopup? _shown;
    private ScreenSize _shownSize;

    /// <summary>Records what the overlay layer was just drawn with.</summary>
    public void MarkShown()
    {
        _shown = current();
        _shownSize = size();
    }

    /// <summary>Redraws when the popup or the screen size differs from what was last drawn.</summary>
    public void Sync()
    {
        var popup = current();
        var same = popup is null ? _shown is null : popup.SameText(_shown);
        if (!same || _shownSize != size())
        {
            show();
        }
    }
}
