using Gitunia.Core.Mouse;
using Gitunia.Tui.App.Pick;
using Gitunia.Tui.Rendering.Overlays;

namespace Gitunia.Tui.App.Overlays.Clicks;

/// <summary>A click on the mainline question; it owns every click while it is open and nothing covers it, also one outside its rows.</summary>
public sealed class MainlineClickTarget(PickActions pick, OverlayCover cover) : IOverlayClickTarget
{
    /// <inheritdoc />
    public OverlayClick? ClickAt(int line, int column)
    {
        if (pick.Prompt is not { } prompt || cover.IsMainlineCovered())
        {
            return null;
        }

        if (MainlineFrameBuilder.ParentAt(line, prompt.Parents.Count) is not { } parent)
        {
            return new OverlayClick(null, GuardsDoubleClick: false);
        }

        return new OverlayClick(DefaultMouseMap.MainlineParentRow, parent);
    }
}
