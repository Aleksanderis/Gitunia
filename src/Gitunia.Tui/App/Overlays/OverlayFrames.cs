using Gitunia.Tui.App.Blame;
using Gitunia.Tui.App.Reflog;
using Gitunia.Tui.Rendering.Overlays;

namespace Gitunia.Tui.App.Overlays;

/// <summary>The frame to draw over the screen: the first of the open overlays, in the order they cover each other.</summary>
/// <param name="editors">The settings screen and the two editors.</param>
/// <param name="reflog">The reflog list.</param>
/// <param name="blame">The blame view.</param>
/// <param name="overlays">The help, palette, menu, dialogs and the which-key popup.</param>
/// <param name="popups">The prompt popup and the mainline question.</param>
/// <param name="cover">Which overlays cover the conflict editor.</param>
public sealed class OverlayFrames(
    EditorOverlays editors,
    ReflogInput reflog,
    BlameInput blame,
    OverlayController overlays,
    PopupFrames popups,
    OverlayCover cover)
{
    /// <summary>The frame, or null when nothing is showing.</summary>
    public OverlayFrame? Current()
    {
        var overlay = editors.Settings.CurrentFrame()
            ?? editors.Conflict.CurrentFrame(cover.IsOtherThanConflictEditorOpen())
            ?? editors.Rebase.CurrentFrame()
            ?? reflog.CurrentFrame()
            ?? blame.CurrentFrame()
            ?? overlays.CurrentFrame();

        return overlay ?? popups.MainlineFrame() ?? popups.PopupFrame();
    }
}
