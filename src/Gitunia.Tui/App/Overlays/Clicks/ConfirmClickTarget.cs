using Gitunia.Core.Mouse;
using Gitunia.Core.Overlays;

namespace Gitunia.Tui.App.Overlays.Clicks;

/// <summary>A click on a button of the confirm dialog.</summary>
public sealed class ConfirmClickTarget(OverlayHitTests hits) : IOverlayClickTarget
{
    /// <inheritdoc />
    public OverlayClick? ClickAt(int line, int column)
    {
        if (hits.ConfirmAnswerAt(line) is not { } answer)
        {
            return null;
        }

        var view = answer == ConfirmAnswer.Yes ? DefaultMouseMap.ConfirmYesButton : DefaultMouseMap.ConfirmNoButton;

        return new OverlayClick(view);
    }
}
