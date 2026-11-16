using Gitunia.App.Blame;
using Gitunia.Core.Editing;
using Gitunia.Core.Overlays;
using Gitunia.Platform;
using Gitunia.Tui.App.Blame;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Conflicts;
using Gitunia.Tui.App.FileHistory;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Pick;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.App.Reflog;
using Gitunia.Tui.App.Settings;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.App.Blame;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Overlays;

public sealed class PopupFallbackLineTests : IDisposable, IPromptDisplay
{
    private readonly TestShell _shell = new();
    private readonly WhichKeyTimer _timer = new(TimeSpan.FromHours(1), InlineUi.Instance);
    private readonly FakePaneChrome _chrome = new(new ScreenSize(120, 36));
    private readonly PopupFrames _frames;

    public PopupFallbackLineTests()
    {
        var host = _shell.Host;
        var store = new OverlayStore(host, _shell.Statuses, _timer, new TestFocusedView(() => "Graph"));
        var settings = new SettingsScreenInput(host, GraphGlyphSet.Unicode);
        var pick = new PickActions(host, new NoPicks());
        var prompts = new PromptDisplays([this]);
        var others = new OtherOverlays(
            store,
            new ListScreens(
                new ReflogInput(host, new NoSelection(), GraphGlyphSet.Unicode),
                new BlameInput(host, new BlameStore(), new NoBlameTargets(), GraphGlyphSet.Unicode),
                new FileHistoryInput(host, GraphGlyphSet.Unicode)),
            settings,
            pick,
            prompts);
        var editors = new EditorOverlays(
            new RebaseEditorInput(host, new NoSelection(), others, GraphGlyphSet.Unicode),
            new ConflictEditorInput(host, GraphGlyphSet.Unicode),
            settings);
        var cover = new OverlayCover(store, others, editors, host, new MouseGate());
        var config = new LiveConfig(
            new ConfigStartup(Path.Combine(Path.GetTempPath(), "gitunia-no-config"), Path.GetTempPath()).Load(),
            new ConsoleCapabilities(false, ColorDepth.None, true),
            Path.GetTempPath());
        _frames = new PopupFrames(pick, prompts, _chrome, config, cover);
    }

    public PromptLine StatusLine => PromptLine.Empty;

    public PromptPopup? Popup { get; set; }

    public void Dispose() => _timer.Dispose();

    [Fact]
    public void GivenNoPopup_WhenAskingForTheFallback_ThenItIsEmpty()
    {
        _chrome.ScreenSize = new ScreenSize(20, 6);

        Assert.True(_frames.PopupFallbackLine().IsEmpty);
    }

    [Fact]
    public void GivenAPopupThatFits_WhenAskingForTheFallback_ThenItIsEmptyBecauseThePopupShows()
    {
        Popup = PromptPopup.ForText("New branch", "From main", "feat", "create");

        Assert.NotNull(_frames.PopupFrame());
        Assert.True(_frames.PopupFallbackLine().IsEmpty);
    }

    [Fact]
    public void GivenATextPopupOnATinyScreen_WhenAskingForTheFallback_ThenTheStatusBarGetsTheFieldWithItsCaret()
    {
        Popup = PromptPopup.ForText("New branch", "From main", "feat", "create") with { InputCaret = 2 };
        _chrome.ScreenSize = new ScreenSize(20, 6);

        var line = _frames.PopupFallbackLine();

        Assert.Null(_frames.PopupFrame());
        Assert.Equal("New branch: ", line.Prefix);
        Assert.Equal("feat", line.Text);
        Assert.Equal(2, line.Caret);
        Assert.Contains("create", line.Suffix, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAChoicePopupOnATinyScreen_WhenAskingForTheFallback_ThenTheStatusBarGetsPlainTextWithNoCaret()
    {
        Popup = PromptPopup.ForChoice("Discard", "Discard changes?", ["y  yes"]);
        _chrome.ScreenSize = new ScreenSize(20, 6);

        var line = _frames.PopupFallbackLine();

        Assert.Null(line.Caret);
        Assert.StartsWith("Discard  ", line.Plain, StringComparison.Ordinal);
    }

    private sealed class NoSelection : IBranchSelectionSource
    {
        public BranchSelection Current() => new(null, null, null);
    }

    private sealed class NoPicks : IPickSelectionSource
    {
        public PickSelection Current() => new(null, []);

        public void EndRange()
        {
        }
    }
}
