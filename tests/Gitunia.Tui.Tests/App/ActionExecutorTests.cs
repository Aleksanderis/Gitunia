using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Input;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.App;

/// <summary>A key through the real reducer and executor: the first handler in the order that takes it wins.</summary>
[Trait("Os", "Independent")]
public class ActionExecutorTests : IInputFocusSource
{
    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly Dictionary<InputHandler, IActionHandler> _handlers = [];
    private readonly RecordingRepeat _repeat = new();
    private readonly List<string> _asked = [];
    private readonly ActionExecutor _executor;
    private readonly ActionInput _input;
    private InputFocus _focus = new(OverlayKind.None, true);

    public ActionExecutorTests()
    {
        var textKeys = new Dictionary<InputHandler, ITextKeyHandler>();
        foreach (var handler in Enum.GetValues<InputHandler>())
        {
            _handlers[handler] = new Recorder(handler, _asked);
            textKeys[handler] = new IgnoringTextKeys();
        }

        // The table keeps the dictionary, so a test can put another handler in place of one.
        _executor = new ActionExecutor(new InputHandlers(_handlers, textKeys), _repeat, new NoSessions());
        _host.EffectRaised += effect => _executor.Run((RouteActionEffect)effect);
        _input = new ActionInput(_host, this);
    }

    InputFocus IInputFocusSource.Current() => _focus;

    [Fact]
    public void GivenNobodyTakesTheAction_WhenInvoked_ThenEveryHandlerIsAskedInTheReducersOrder()
    {
        _input.Route(ActionIds.ListDown, null);

        var expected = InputRouting.Handlers(ActionIds.ListDown, AppState.Empty.Shell, _focus);
        Assert.Equal(expected.Select(handler => handler.ToString()), _asked);
    }

    [Fact]
    public void GivenAnOpenPromptTakesTheAction_WhenInvoked_ThenNoPaneIsAskedAndNoRepeatStarts()
    {
        _handlers[InputHandler.OpenPrompt] = new Recorder(InputHandler.OpenPrompt, _asked) { Takes = true };

        _input.Route(ActionIds.InputAccept, null);

        Assert.Equal(nameof(InputHandler.OpenPrompt), _asked[^1]);
        Assert.DoesNotContain(nameof(InputHandler.Sidebar), _asked);
        Assert.DoesNotContain(nameof(InputHandler.Navigation), _asked);
        Assert.Equal([ActionIds.InputAccept], _repeat.Started);
    }

    [Fact]
    public void GivenAnOverlayTakesTheAction_WhenInvoked_ThenThePanesAreNotAskedAndNoRepeatStarts()
    {
        _handlers[InputHandler.Overlays] = new Recorder(InputHandler.Overlays, _asked) { Takes = true };

        _input.Route(ActionIds.ListDown, null);

        Assert.Equal(nameof(InputHandler.Overlays), _asked[^1]);
        Assert.Empty(_repeat.Started);
        Assert.Equal(string.Empty, _executor.Message);
    }

    [Fact]
    public void GivenAPaneTakesTheActionWithANotice_WhenInvoked_ThenTheNoticeIsTheMessageUntilTheNextAction()
    {
        var diff = new Recorder(InputHandler.Diff, _asked) { Takes = true, Notice = "No next file" };
        _handlers[InputHandler.Diff] = diff;

        _input.Route(ActionIds.ListDown, null);

        Assert.Equal("No next file", _executor.Message);
        Assert.Equal([ActionIds.ListDown], _repeat.Started);

        _handlers[InputHandler.Diff] = new Recorder(InputHandler.Diff, _asked) { Takes = true };
        _input.Route(ActionIds.ListUp, null);

        Assert.Equal(string.Empty, _executor.Message);
    }

    [Fact]
    public void GivenAScreenTakesTheActionWithANotice_WhenInvoked_ThenTheMessageIsNotChanged()
    {
        var settings = new Recorder(InputHandler.Settings, _asked) { Takes = true, Notice = "opened" };
        _handlers[InputHandler.Settings] = settings;

        _input.Route(ActionIds.ListDown, null);

        Assert.Equal(string.Empty, _executor.Message);
    }

    [Fact]
    public void GivenAnOpenPaletteAndSettingsRequested_WhenInvoked_ThenNobodyIsAsked()
    {
        _focus = new InputFocus(OverlayKind.Palette, true);

        _input.Route(ActionIds.SettingsOpen, null);

        Assert.Empty(_asked);
        Assert.Empty(_repeat.Started);
    }

    [Fact]
    public void GivenTheDetailPaneIsFocused_WhenEscIsInvoked_ThenTheSearchPromptIsStillAsked()
    {
        _focus = new InputFocus(OverlayKind.None, false);

        _input.Route(ActionIds.Cancel, null);

        Assert.Contains(nameof(InputHandler.Search), _asked);
    }

    [Fact]
    public void GivenTheDetailPaneIsFocused_WhenAPlainActionIsInvoked_ThenTheSearchPromptIsNotAsked()
    {
        _focus = new InputFocus(OverlayKind.None, false);

        _input.Route(ActionIds.ListDown, null);

        Assert.DoesNotContain(nameof(InputHandler.Search), _asked);
    }

    [Fact]
    public void GivenAnActionWithACount_WhenInvoked_ThenTheHandlerGetsTheCount()
    {
        var recorder = new Recorder(InputHandler.Navigation, _asked) { Takes = true };
        _handlers[InputHandler.Navigation] = recorder;

        _input.Route(ActionIds.ListDown, 7);

        Assert.Equal(7, recorder.LastCount);
    }

    [Fact]
    public void GivenNoHandlerIsRegisteredForACase_WhenTheTableIsBuilt_ThenItIsAProgrammingError()
    {
        var empty = new Dictionary<InputHandler, IActionHandler>();

        Assert.Throws<InvalidOperationException>(
            () => new InputHandlers(empty, new Dictionary<InputHandler, ITextKeyHandler>()));
    }

    private sealed class Recorder(InputHandler handler, List<string> asked) : IActionHandler, IActionNotice
    {
        public bool Takes { get; init; }

        public string Notice { get; init; } = string.Empty;

        public int? LastCount { get; private set; }

        public bool TryExecute(ActionId action, int? count)
        {
            asked.Add(handler.ToString());
            LastCount = count;

            return Takes;
        }
    }

    private sealed class IgnoringTextKeys : ITextKeyHandler
    {
        public bool HandleTextKey(KeyEvent key) => false;
    }

    private sealed class NoSessions : IActiveSessions
    {
        public TerminalTabSession? ActiveSession() => null;
    }

    private sealed class RecordingRepeat : IRepeatStarter
    {
        public List<ActionId> Started { get; } = [];

        public void AfterPaneAction(ActionId action) => Started.Add(action);
    }
}
