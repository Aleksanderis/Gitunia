using Gitunia.Tui.Rendering.Overlays;

namespace Gitunia.Tui.App.Overlays;

/// <summary>The frame to draw over the screen: the first of the open overlays, in the order they cover each other.</summary>
/// <param name="editors">The settings screen and the two editors.</param>
/// <param name="screens">The reflog list and the file history.</param>
/// <param name="overlays">The help, palette, menu, dialogs and the which-key popup.</param>
/// <param name="popups">The prompt popup and the mainline question.</param>
/// <param name="cover">Which overlays cover the conflict editor.</param>
public sealed class OverlayFrames(
    EditorOverlays editors,
    ListScreens screens,
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
            ?? screens.Reflog.CurrentFrame()
            ?? screens.FileHistory.CurrentFrame()
            ?? overlays.CurrentFrame();

        return overlay ?? popups.MainlineFrame() ?? popups.PopupFrame();
    }
}
