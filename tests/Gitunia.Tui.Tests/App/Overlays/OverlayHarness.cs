using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Platform;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Tests.Fakes;

namespace Gitunia.Tui.Tests.App.Overlays;

/// <summary>
/// The overlay store, controller and hit tests as the application wires them, with the settings a test changes (the
/// command level, the which-key popup, the row menu) behind plain members.
/// </summary>
public sealed class OverlayHarness : IRowMenuTargets
{
    private readonly OverlayStore _store;
    private readonly OverlayController _controller;
    private readonly OverlayHitTests _hits;
    private readonly LiveConfig _config;
    private Func<CommandLevel> _level = static () => CommandLevel.AdvancedPlus;

    public OverlayHarness(
        KeymapEngine engine,
        KeyController keys,
        WhichKeyTimer timer,
        IActionRoute route,
        FakePaneChrome chrome,
        IFocusedView baseView,
        TestShell shell)
    {
        _config = new LiveConfig(
            new ConfigStartup(Path.Combine(Path.GetTempPath(), "gitunia-no-config"), Path.GetTempPath()).Load(),
            new ConsoleCapabilities(false, ColorDepth.None, true),
            Path.GetTempPath());
        _store = new OverlayStore(shell.Host, shell.Statuses, timer, baseView);
        _controller = new OverlayController(
            _store,
            keys,
            route,
            shell.Host,
            new OverlayServices(engine, chrome, _config, this, shell.Statuses));
        _hits = new OverlayHitTests(_store, _controller, chrome);
    }

    /// <summary>Raised when what the overlay layer shows may have changed.</summary>
    public event Action Changed
    {
        add => _store.Changed += value;
        remove => _store.Changed -= value;
    }

    /// <summary>What the actions menu offers for a view; the default offers nothing.</summary>
    public Func<string, RowMenuTarget?> MenuTarget { get; set; } = _ => null;

    /// <summary>The command level, asked again on every use.</summary>
    public Func<CommandLevel> Level
    {
        set => _level = value;
    }

    /// <summary>Whether a pending chord opens the which-key popup.</summary>
    public bool WhichKeyEnabled
    {
        set
        {
            _config.ChangeOptions(_config.Options with { WhichKeyPopup = value });
            _controller.OnOptionsChanged();
        }
    }

    public OverlayState State
    {
        get
        {
            ApplyLevel();

            return _store.State;
        }
    }

    public string ViewName => _store.ViewName;

    RowMenuTarget? IRowMenuTargets.TargetOf(string view, ActionId action) => MenuTarget(view);

    public OverlayFrame? CurrentFrame()
    {
        ApplyLevel();

        return _controller.CurrentFrame();
    }

    public bool Handle(ActionId action, int? count)
    {
        ApplyLevel();

        return _controller.Handle(action, count);
    }

    public void OnScreenResized() => _controller.OnScreenResized();

    public ConfirmAnswer? ConfirmAnswerAt(int frameLine) => _hits.ConfirmAnswerAt(frameLine);

    public bool SelectPaletteEntryAt(int frameLine) => _hits.SelectPaletteEntryAt(frameLine);

    public bool SelectMenuEntryAt(int frameLine) => _hits.SelectMenuEntryAt(frameLine);

    public int? HelpTabNumberAt(int frameLine, int frameColumn) => _hits.HelpTabNumberAt(frameLine, frameColumn);

    private void ApplyLevel() => _config.ChangeOptions(_config.Options with { CommandLevel = _level() });
}
