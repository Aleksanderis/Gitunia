using Gitunia.Core.Graph;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Screen;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The seventh stage: what the main view draws and the views that follow the state. The main view is built here, after
/// everything it reads, and nothing built earlier needs it.
/// </summary>
public sealed class ScreenParts
{
    /// <param name="foundation">The configuration, the state and the widgets.</param>
    /// <param name="panes">The controllers of the panes.</param>
    /// <param name="overlays">The prompts, editors and screens.</param>
    /// <param name="keys">The key controller and the overlay controller.</param>
    /// <param name="handlers">The action handlers.</param>
    public ScreenParts(
        ShellFoundation foundation,
        PaneParts panes,
        OverlayParts overlays,
        KeyParts keys,
        HandlerParts handlers)
    {
        var host = foundation.Host;
        var config = foundation.Config;
        Messages = new StatusMessages(
            host,
            foundation.Statuses,
            panes.Creation,
            new ActionNotices(panes.WorkspaceActions, handlers.Actions));
        Popups = new PopupFrames(overlays.Pick, overlays.Prompts, foundation.Chrome, config, overlays.Cover);
        var hints = new StatusHints(
            host,
            overlays.Store,
            overlays.Editors,
            overlays.Screens,
            new HintContext(keys.Repeat, panes.DiffView, config));
        var lines = new StatusLines(hints, overlays.Prompts, Popups, keys.Repeat);
        Content = new ScreenContent(
            host,
            new ScreenModelSource(new ScreenModelParts(
                host,
                foundation.Registry,
                foundation.Panes,
                keys.Keys,
                Messages,
                panes.Picker,
                config,
                lines)),
            foundation.Sessions,
            foundation.Panes,
            new PaneTitleParts(keys.Repeat, panes.DiffView, panes.WorkingDiff));
        var mode = new ScreenMode();
        var grids = new FrameGrids(foundation.Panes, foundation.Geometry, mode, Content);
        var parts = new ChromeParts(foundation.Panes, foundation.Geometry, grids, Content, foundation.Gate, mode);
        View = new MainView(parts, new WorkingTreeChrome(parts, panes.ListButtons));
        Binder = new GraphTabBinder(
            host,
            foundation.Registry,
            foundation.Panes,
            new GraphListSource(new CommitGraph(), config.Glyphs),
            foundation.Chrome);
        Focus = new PaneFocusCoordinator(host, foundation.Panes, View);
        Picker = new PickerPresenter(panes.Picker, keys.Keys, panes.Visual, View, Focus);
        PopupTracker = new PromptPopupTracker(overlays.Prompts, foundation.Chrome);
        Frames = new OverlayPresenter(
            host,
            foundation.Panes,
            new OverlayFrames(overlays.Editors, overlays.Screens, keys.Overlays, Popups, overlays.Cover),
            PopupTracker);
        SizeDragger = new PaneSizeDragger(
            host,
            foundation.Arrangements,
            foundation.Chrome,
            foundation.Sessions,
            overlays.KeyFocus);
    }

    /// <summary>The message of the status bar.</summary>
    public StatusMessages Messages { get; }

    /// <summary>The prompt popup and the mainline question frames.</summary>
    public PopupFrames Popups { get; }

    /// <summary>What the main view draws.</summary>
    public ScreenContent Content { get; }

    /// <summary>The chrome around the panes.</summary>
    public MainView View { get; }

    /// <summary>Keeps the graph list showing the active tab.</summary>
    public GraphTabBinder Binder { get; }

    /// <summary>Keeps the toolkit focus and the tab's focused pane in step.</summary>
    public PaneFocusCoordinator Focus { get; }

    /// <summary>Follows the picker state.</summary>
    public PickerPresenter Picker { get; }

    /// <summary>Asks for a redraw when a prompt popup changed.</summary>
    public PromptPopupTracker PopupTracker { get; }

    /// <summary>Draws the overlay layer.</summary>
    public OverlayPresenter Frames { get; }

    /// <summary>Divider drags and the layout after a size change.</summary>
    public PaneSizeDragger SizeDragger { get; }
}
