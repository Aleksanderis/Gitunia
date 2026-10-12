using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Rendering.Overlays;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

public class OverlayFrameTests
{
    private static readonly KeyBindingTable Table = DefaultKeymap.Create();

    private static string Text(OverlayFrame? frame) =>
        frame is null ? "<none>" : string.Join('\n', frame.Lines.Select(line => line.Text));

    private static PaletteState Palette() => new(PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView));

    private static IReadOnlyList<HelpSection> Help() => HelpBuilder.Build(Table, DefaultKeymap.GraphView);

    [Fact]
    public void GivenMessagesOverlay_WhenRendering_ThenItListsSeverityAndTextWithTheMessagesTitle()
    {
        var sections = new List<HelpSection> { new("Newest first", [new HelpRow("error", "repo: could not read the stashes"), new HelpRow("info", "ok")]) };
        var state = OverlayState.OpenMessages(KeyMode.Normal);

        var frame = OverlayFrameFactory.Create(state, sections, string.Empty, [], 60, 10, GraphGlyphSet.Unicode);

        var text = Text(frame);
        Assert.Contains("Status messages", text, StringComparison.Ordinal);
        Assert.Contains("error  repo: could not read the stashes", text, StringComparison.Ordinal);
        Assert.Contains("Esc close", text, StringComparison.Ordinal);
        Assert.NotNull(OverlayFrameFactory.Create(state, sections, string.Empty, [], 20, 6, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenPalette_WhenRenderingAt80x24_ThenItListsActionsWithTheirKeys()
    {
        var frame = PaletteFrameBuilder.Build(Palette(), 80, 24, GraphGlyphSet.Unicode);

        Assert.Equal(
            string.Join(
                '\n',
                " : _                                                                    ",
                "│ Jump to HEAD                                                       @ │",
                "│ Jump to a ref or hash                                             gr │",
                "│ Focus the commit details                                       Enter │",
                "│ Copy the commit hash                                               y │",
                "│ Refresh the repository                                             r │",
                "│ Move down                                                   j / Down │",
                "│ Move up                                                       k / Up │",
                "│ Go to the top                                                     gg │",
                "│ Go to the bottom                                                   G │",
                "│ Search                                                             / │",
                "│ Next search match                                                  n │",
                "│ Previous search match                                              N │",
                "│ 1/70  Up/Down choose  Enter run  Esc close                           │",
                "╰──────────────────────────────────────────────────────────────────────╯"),
            Text(frame));
        Assert.Equal(4, frame!.Column);
    }

    [Fact]
    public void GivenFilter_WhenRenderingAt60x12_ThenOnlyMatchesShowAndTheSelectionIsMarked()
    {
        var palette = Palette().Type("tab").Move(1);

        var frame = PaletteFrameBuilder.Build(palette, 60, 12, GraphGlyphSet.Unicode);

        Assert.Equal(
            string.Join(
                '\n',
                " : tab_                                                   ",
                "│ Next tab                                            gt │",
                "│ Previous tab                                        gT │",
                "│ Go to tab 1                                      Alt-1 │",
                "│ Go to tab 2                                      Alt-2 │",
                "│ Go to tab 3                                      Alt-3 │",
                "│ Go to tab 4                                      Alt-4 │",
                "│ Go to tab 5                                      Alt-5 │",
                "│ 2/16  Up/Down choose  Enter run  Esc close             │",
                "╰────────────────────────────────────────────────────────╯"),
            Text(frame));
        Assert.Equal([false, true, false], frame!.Lines.Skip(1).Take(3).Select(line => line.IsSelected).ToArray());
    }

    [Fact]
    public void GivenNothingMatches_WhenRenderingThePalette_ThenItSaysSo()
    {
        var frame = PaletteFrameBuilder.Build(Palette().Type("zzzzqq"), 40, 10, GraphGlyphSet.Ascii);

        Assert.Equal(
            string.Join(
                '\n',
                " : zzzzqq_                            ",
                "| no matching action                 |",
                "| 0/0  Up/Down choose  Enter run  Es~|",
                "+------------------------------------+"),
            Text(frame));
    }

    [Fact]
    public void GivenHelpForTheGraph_WhenRenderingAt80x24_ThenItListsTheViewsKeysFromTheKeymap()
    {
        var frame = HelpFrameBuilder.Build(Help(), DefaultKeymap.GraphView, 0, 80, 24, GraphGlyphSet.Unicode);

        Assert.Equal(
            string.Join(
                '\n',
                " Keys: Graph                                                    ",
                "│ Graph                                                        │",
                "│  @                     Jump to HEAD                          │",
                "│  gr                    Jump to a ref or hash                 │",
                "│  Enter                 Focus the commit details              │",
                "│  y                     Copy the commit hash                  │",
                "│  r                     Refresh the repository                │",
                "│                                                              │",
                "│ Lists                                                        │",
                "│  j / Down              Move down                             │",
                "│  k / Up                Move up                               │",
                "│  gg                    Go to the top                         │",
                "│  G                     Go to the bottom                      │",
                "│  /                     Search                                │",
                "│  n                     Next search match                     │",
                "│  N                     Previous search match                 │",
                "│  Ctrl-d / PageDown     Half page down                        │",
                "│  Ctrl-u / PageUp       Half page up                          │",
                "│  v                     Start or end range selection          │",
                "│                                                              │",
                "│ 1-19/48  list keys scroll  Esc close                         │",
                "╰──────────────────────────────────────────────────────────────╯"),
            Text(frame));
    }

    [Fact]
    public void GivenHelpScrolledToTheEnd_WhenRenderingAt40x10_ThenTheLastRowsShow()
    {
        var sections = Help();
        var maximum = HelpFrameBuilder.MaximumScroll(sections, 40, 10);

        var frame = HelpFrameBuilder.Build(sections, DefaultKeymap.GraphView, maximum + 50, 40, 10, GraphGlyphSet.Unicode);

        Assert.Contains("Zoom the fo", Text(frame), StringComparison.Ordinal);
        Assert.Equal(10 - 2, frame!.Height);
    }

    [Fact]
    public void GivenWhichKeyAfterG_WhenRenderingAt60x12_ThenThePopupSitsAboveTheStatusBar()
    {
        var engine = new KeymapEngine(Table);
        var state = engine.Process(KeymapState.Initial, new KeyEvent("g"), DefaultKeymap.GraphView).State;
        var rows = WhichKeyBuilder.Build(engine.Continuations(state, DefaultKeymap.GraphView));

        var frame = WhichKeyFrameBuilder.Build("g", rows, 60, 12, GraphGlyphSet.Unicode);

        Assert.Equal(
            string.Join(
                '\n',
                " g-                          ",
                "│ r  Jump to a ref or hash  │",
                "│ g  Go to the top          │",
                "│ t  Next tab               │",
                "│ T  Previous tab           │",
                "╰───────────────────────────╯"),
            Text(frame));
        Assert.Equal(60 - frame!.Width, frame.Column);
        Assert.Equal(11 - frame.Height, frame.Row);
    }

    [Theory]
    [InlineData(80, 24)]
    [InlineData(40, 12)]
    [InlineData(30, 8)]
    [InlineData(20, 6)]
    [InlineData(10, 4)]
    [InlineData(5, 2)]
    public void GivenAnyScreenSize_WhenRenderingOverlays_ThenFramesFitTheScreenAndEveryLineIsTheSameWidth(int width, int height)
    {
        var frames = new[]
        {
            PaletteFrameBuilder.Build(Palette(), width, height, GraphGlyphSet.Unicode),
            HelpFrameBuilder.Build(Help(), "Graph", 0, width, height, GraphGlyphSet.Unicode),
            WhichKeyFrameBuilder.Build("g", [new WhichKeyRow("g", "Go to the top")], width, height, GraphGlyphSet.Unicode),
        };

        foreach (var frame in frames.OfType<OverlayFrame>())
        {
            Assert.True(frame.Column >= 0 && frame.Row >= 0);
            Assert.True(frame.Column + frame.Width <= width);
            Assert.True(frame.Row + frame.Height <= height);
            Assert.All(frame.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
        }
    }

    [Fact]
    public void GivenTinyScreen_WhenRenderingThePalette_ThenNothingIsDrawn()
    {
        Assert.Null(PaletteFrameBuilder.Build(Palette(), 10, 4, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenOverlayState_WhenCreatingAFrame_ThenTheOpenOverlayWinsOverTheWhichKeyPopup()
    {
        var state = OverlayState.OpenPalette(Palette(), KeyMode.Normal).WithWhichKey(true);

        var frame = OverlayFrameFactory.Create(state, [], "g", [new WhichKeyRow("g", "Go to the top")], 80, 24, GraphGlyphSet.Unicode);

        Assert.StartsWith(" : _", frame!.Lines[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoOverlayAndNoPopup_WhenCreatingAFrame_ThenThereIsNone()
    {
        Assert.Null(OverlayFrameFactory.Create(OverlayState.None, [], string.Empty, [], 80, 24, GraphGlyphSet.Unicode));
    }
}
