using Gitunia.Core.Settings;
using Gitunia.App;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Rebase;
using Gitunia.Core.Blame;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Reflog;
using Gitunia.Core.Shell;
using Gitunia.Core.Pick;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// Draws the overlay layer: shows the frame again whenever an overlay changed or the state behind one did. The conflict
/// editor, the reflog list, the settings screen and the mainline question live in the shell state, so a result that
/// arrives later redraws them.
/// </summary>
public sealed class OverlayPresenter
{
    private readonly Dispatcher _host;
    private readonly PaneViews _panes;
    private readonly OverlayFrames _frames;
    private readonly PromptPopupTracker _popups;
    private ConflictSession? _drawnConflict;
    private ReflogSession? _drawnReflog;
    private BlameSession? _drawnBlame;
    private FileHistorySession? _drawnFileHistory;
    private RebaseEditorSession? _drawnRebaseEditor;
    private MainlineQuestion? _drawnMainline;
    private ScreenExtent? _drawnScreen;
    private SettingsSession? _drawnSettings;

    /// <param name="host">The shell state the overlays live in.</param>
    /// <param name="panes">The overlay box.</param>
    /// <param name="frames">Builds the frame to show.</param>
    /// <param name="popups">Asks for a redraw when a prompt popup changed.</param>
    public OverlayPresenter(Dispatcher host, PaneViews panes, OverlayFrames frames, PromptPopupTracker popups)
    {
        _host = host;
        _panes = panes;
        _frames = frames;
        _popups = popups;
        popups.RedrawRequested += Show;
    }

    /// <summary>Shows the frame of the overlays that are open now.</summary>
    public void Show()
    {
        var frame = _frames.Current();
        _popups.MarkShown(frame is not null);
        _panes.Overlay.Show(frame);
    }

    /// <summary>Shows the frame and redraws the chrome (the mode and the hints changed with it).</summary>
    public void Refresh()
    {
        Show();
        _panes.Chrome.RefreshChrome();
    }

    /// <summary>Redraws the overlays whose session in the shell state changed.</summary>
    public void RefreshOnStateChange()
    {
        var shell = _host.State.Shell;
        if (ReferenceEquals(shell.Conflict, _drawnConflict)
            && ReferenceEquals(shell.Reflog, _drawnReflog)
            && ReferenceEquals(shell.Blame, _drawnBlame)
            && ReferenceEquals(shell.FileHistory, _drawnFileHistory)
            && ReferenceEquals(shell.Settings, _drawnSettings)
            && ReferenceEquals(shell.RebaseEditor, _drawnRebaseEditor)
            && ReferenceEquals(shell.Mainline, _drawnMainline) && !MainlineNeedsLayout(shell))
        {
            return;
        }

        _drawnConflict = shell.Conflict;
        _drawnReflog = shell.Reflog;
        _drawnBlame = shell.Blame;
        _drawnFileHistory = shell.FileHistory;
        _drawnRebaseEditor = shell.RebaseEditor;
        _drawnSettings = shell.Settings;
        _drawnMainline = shell.Mainline;
        _drawnScreen = shell.Screen;
        Refresh();
    }

    // An open mainline question is placed again when the window is resized.
    private bool MainlineNeedsLayout(ShellState shell) =>
        shell.Mainline is not null && !Equals(shell.Screen, _drawnScreen);
}
