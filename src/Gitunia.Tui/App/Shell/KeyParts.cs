using Gitunia.Core.Mouse;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Overlays.Clicks;
using Gitunia.Tui.App.Panes;
using Gitunia.Core.Input;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The fifth stage: the key controller with the repeat mode and the typed-key router, the overlay controller that listens
/// to it, and the mouse controller with the click routing of the overlays.
/// </summary>
public sealed class KeyParts
{
    /// <param name="foundation">The configuration, the state and the widgets.</param>
    /// <param name="route">Turns a key into an action.</param>
    /// <param name="panes">The controllers of the panes.</param>
    /// <param name="overlays">The prompts, editors and screens.</param>
    public KeyParts(ShellFoundation foundation, RouteParts route, PaneParts panes, OverlayParts overlays)
    {
        var host = foundation.Host;
        Repeat = new RepeatController(foundation.Config.Keymap.Table, host, overlays.KeyFocus, route.ActionInput);
        Keys = new KeyController(
            foundation.Engine,
            route.ActionInput,
            host,
            Repeat,
            overlays.Handlers.TextKeys());
        Overlays = new OverlayController(
            route.Overlays,
            Keys,
            route.ActionInput,
            host,
            new OverlayServices(foundation.Engine, foundation.Chrome, foundation.Config, panes.Menus, foundation.Statuses));
        Hits = new OverlayHitTests(route.Overlays, Overlays, foundation.Chrome);
        Mouse = new MouseController(DefaultMouseMap.Create(), route.ActionInput, foundation.Gate);
        Clicks = new OverlayClickRouter(ClickTargets(overlays));
        OverlayGestures = new OverlayGestures(
            Mouse,
            Clicks,
            overlays.Guard,
            overlays.Editors,
            overlays.Cover,
            overlays.Blame);
        GestureRouter = new GestureRouter(
            Mouse,
            panes.Visual,
            new GestureServices(overlays.Guard, overlays.Cover, overlays.Editors, foundation.Panes));
        Quit = new QuitRequests(host);
        var ctrlC = new CtrlCHandler(host, panes.Visual, Quit);
        Input = new KeyInput(host, Keys, ctrlC, overlays.KeyFocus, overlays.Editors);
    }

    /// <summary>The sticky repeat mode.</summary>
    public RepeatController Repeat { get; }

    /// <summary>The keymap engine and its mode.</summary>
    public KeyController Keys { get; }

    /// <summary>The help, palette, menu and dialogs.</summary>
    public OverlayController Overlays { get; }

    /// <summary>Hit tests of a click on an overlay frame.</summary>
    public OverlayHitTests Hits { get; }

    /// <summary>Turns a gesture into an action.</summary>
    public MouseController Mouse { get; }

    /// <summary>Finds the overlay under a click.</summary>
    public OverlayClickRouter Clicks { get; }

    /// <summary>The mouse on an open overlay.</summary>
    public OverlayGestures OverlayGestures { get; }

    /// <summary>Prepares a gesture on a pane or the chrome.</summary>
    public GestureRouter GestureRouter { get; }

    /// <summary>Asks the application to quit.</summary>
    public QuitRequests Quit { get; }

    /// <summary>The key presses and pastes of the terminal.</summary>
    public KeyInput Input { get; }

    private Dictionary<OverlayClickTarget, IOverlayClickTarget> ClickTargets(OverlayParts overlays) => new()
    {
        [OverlayClickTarget.Settings] = new SettingsClickTarget(overlays.Settings),
        [OverlayClickTarget.ConflictEditor] = new ConflictEditorClickTarget(overlays.ConflictEditor, overlays.Cover),
        [OverlayClickTarget.RebaseEditor] = new RebaseEditorClickTarget(overlays.RebaseEditor),
        [OverlayClickTarget.Reflog] = new ReflogClickTarget(overlays.Reflog),
        [OverlayClickTarget.Blame] = new BlameClickTarget(overlays.Blame),
        [OverlayClickTarget.FileHistory] = new FileHistoryClickTarget(overlays.FileHistory),
        [OverlayClickTarget.HelpTab] = new HelpTabClickTarget(Hits),
        [OverlayClickTarget.Palette] = new PaletteClickTarget(Hits),
        [OverlayClickTarget.ContextMenu] = new ContextMenuClickTarget(Hits),
        [OverlayClickTarget.Mainline] = new MainlineClickTarget(overlays.Pick, overlays.Cover),
        [OverlayClickTarget.Confirm] = new ConfirmClickTarget(Hits),
    };
}
