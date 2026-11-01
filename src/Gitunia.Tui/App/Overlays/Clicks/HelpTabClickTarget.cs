using Gitunia.Core.Mouse;

namespace Gitunia.Tui.App.Overlays.Clicks;

/// <summary>A click on a tab label of the help.</summary>
public sealed class HelpTabClickTarget(OverlayHitTests hits) : IOverlayClickTarget
{
    /// <inheritdoc />
    public OverlayClick? ClickAt(int line, int column) =>
        hits.HelpTabNumberAt(line, column) is { } helpTab
            ? new OverlayClick(DefaultMouseMap.HelpTabLabel, helpTab)
            : null;
}
