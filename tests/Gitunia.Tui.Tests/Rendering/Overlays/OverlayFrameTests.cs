using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

public class OverlayFrameTests
{
    private static readonly KeyBindingTable Table = DefaultKeymap.Create();

    private static string Text(OverlayFrame? frame) =>
        frame is null ? "<none>" : string.Join('\n', frame.Lines.Select(line => line.Text));

    private static PaletteState Palette() => new(PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView));

    private static IReadOnlyList<HelpSection> Help() => HelpBuilder.Build(Table, DefaultKeymap.GraphView);

    private static ConfirmPrompt DiscardPrompt() => new(
        "Discard changes",
        ["Discard all unstaged changes to src/Gitunia.Tui/App/WorkingTree/DiscardActions.cs?", "Its staged changes are kept."],
        "The changes are removed from the file on disk. Nothing keeps a copy: this cannot be undone.",
        "Discard changes");

    [Fact]
    public void GivenConfirmDialog_WhenRenderingAt80x24_ThenItIsCentredAndNamesWhatIsLost()
    {
        var state = OverlayState.OpenConfirm(DiscardPrompt(), KeyMode.Normal);

        var frame = OverlayFrameFactory.Create(state, [], string.Empty, [], 80, 24, GraphGlyphSet.Unicode);

        Assert.Equal(
            string.Join(
                '\n',
                " Discard changes".PadRight(64),
                "│ Discard all unstaged changes to                              │",
                "│ src/Gitunia.Tui/App/WorkingTree/DiscardActions.cs?           │",
                "│ Its staged changes are kept.                                 │",
                "│                                                              │",
                "│ The changes are removed from the file on disk. Nothing keeps │",
                "│ a copy: this cannot be undone.                               │",
                "│                                                              │",
                "│ y  Discard changes                                           │",
                "│ n  Cancel (Esc does the same)                                │",
                "╰──────────────────────────────────────────────────────────────╯"),
            Text(frame));
        Assert.Equal(8, frame!.Column);
        Assert.Equal(6, frame.Row);
    }

    [Fact]
    public void GivenConfirmDialog_WhenRenderingInAscii_ThenTheBoxUsesPlainCharacters()
    {
        var state = OverlayState.OpenConfirm(DiscardPrompt(), KeyMode.Normal);

        var frame = OverlayFrameFactory.Create(state, [], string.Empty, [], 60, 20, GraphGlyphSet.Ascii);

        Assert.Equal(
            string.Join(
                '\n',
                " Discard changes".PadRight(58),
                "| Discard all unstaged changes to                        |",
                "| src/Gitunia.Tui/App/WorkingTree/DiscardActions.cs?     |",
                "| Its staged changes are kept.                           |",
                "|                                                        |",
                "| The changes are removed from the file on disk. Nothing |",
                "| keeps a copy: this cannot be undone.                   |",
                "|                                                        |",
                "| y  Discard changes                                     |",
                "| n  Cancel (Esc does the same)                          |",
                "+--------------------------------------------------------+"),
            Text(frame));
    }

    [Theory]
    [InlineData(18, 14)]
    [InlineData(30, 16)]
    [InlineData(40, 10)]
    [InlineData(200, 50)]
    public void GivenConfirmDialog_WhenRenderingAtOtherSizes_ThenEveryRowHasTheSameWidthAndFitsTheScreen(int width, int height)
    {
        var state = OverlayState.OpenConfirm(DiscardPrompt(), KeyMode.Normal);

        var frame = OverlayFrameFactory.Create(state, [], string.Empty, [], width, height, GraphGlyphSet.Unicode);

        if (frame is null)
        {
            return;
        }

        Assert.All(frame.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
        Assert.True(frame.Row + frame.Height <= height);
        Assert.True(frame.Column + frame.Width <= width);
        Assert.Contains(" y  Discard changes", Text(frame), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(10, 24)]
    [InlineData(80, 5)]
    public void GivenConfirmDialog_WhenTheScreenIsTooSmall_ThenThereIsNoFrame(int width, int height)
    {
        var state = OverlayState.OpenConfirm(DiscardPrompt(), KeyMode.Normal);

        Assert.Null(OverlayFrameFactory.Create(state, [], string.Empty, [], width, height, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenMessagesOverlay_WhenRendering_ThenItListsSeverityAndTextWithTheMessagesTitle()
    {
        var sections = new List<HelpSection> { new("Newest first", [new HelpRow("error", "repo: could not read the stashes"), new HelpRow("info", "ok")]) };
        var state = OverlayState.OpenMessages(KeyMode.Normal);

        var frame = OverlayFrameFactory.Create(state, sections, string.Empty, [], 60, 10, GraphGlyphSet.Unicode);

        var text = Text(frame);
        Assert.Contains("Status messages", text, StringComparison.Ordinal);
        Assert.Contains("error │ repo: could not read the stashes", text, StringComparison.Ordinal);
        Assert.Contains("Esc close", text, StringComparison.Ordinal);
        Assert.NotNull(OverlayFrameFactory.Create(state, sections, string.Empty, [], 20, 6, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenOutputOverlay_WhenRendering_ThenItShowsItsTitleAndTheOutputLines()
    {
        var sections = new List<HelpSection> { new("Output of git commit", [new HelpRow(string.Empty, "pre-commit: forbidden word"), new HelpRow(string.Empty, "  indented")]) };
        var state = OverlayState.OpenOutput("Commit failed", KeyMode.Insert);

        var frame = OverlayFrameFactory.Create(state, sections, string.Empty, [], 70, 12, GraphGlyphSet.Unicode);

        var text = Text(frame);
        Assert.Contains("Commit failed", text, StringComparison.Ordinal);
        Assert.Contains("pre-commit: forbidden word", text, StringComparison.Ordinal);
        Assert.Contains("Esc close", text, StringComparison.Ordinal);
        Assert.Equal(KeyMode.Insert, state.ReturnMode);
    }

    [Fact]
    public void GivenLongMessage_WhenRenderingMessages_ThenItWrapsAndEveryWordIsReadable()
    {
        const string message = "fatal: unable to access the repository because the object database is corrupt and cannot be read";
        var sections = new List<HelpSection> { new("Newest first", [new HelpRow("error", message)]) };
        var state = OverlayState.OpenMessages(KeyMode.Normal);

        var frame = OverlayFrameFactory.Create(state, sections, string.Empty, [], 40, 20, GraphGlyphSet.Unicode);

        var lines = Text(frame!).Split('\n');
        Assert.All(lines, line => Assert.True(line.Length <= 40));
        var joined = string.Join(' ', lines.Select(line => line.Trim('│', ' ')));
        foreach (var word in message.Split(' '))
        {
            Assert.Contains(word, joined, StringComparison.Ordinal);
        }

        Assert.True(HelpFrameBuilder.ContentHeight(sections, 40) > 2);
    }

    [Fact]
    public void GivenWordLongerThanALine_WhenRendering_ThenItIsCutWithoutSplittingSurrogatesOrCombiningMarks()
    {
        var word = $"{string.Concat(Enumerable.Repeat("\U0001F600", 12))}{new string('e', 10)}\u0301{new string('x', 30)}";
        var sections = new List<HelpSection> { new("S", [new HelpRow("k", word)]) };

        var frame = HelpFrameBuilder.Build(sections, string.Empty, 0, 30, 30, GraphGlyphSet.Unicode);

        var text = Text(frame);
        Assert.DoesNotContain('\uFFFD', text);
        Assert.DoesNotContain("\n│ \u0301", text, StringComparison.Ordinal);
        Assert.All(text.Split('\n'), line => Assert.False(char.IsLowSurrogate(line.TrimEnd().LastOrDefault()) && !char.IsHighSurrogate(line.TrimEnd().Length > 1 ? line.TrimEnd()[^2] : 'a')));
        Assert.Equal(string.Concat(Enumerable.Repeat("\U0001F600", 12)).Length, text.Split("\U0001F600").Length > 1 ? (text.Split("\U0001F600").Length - 1) * 2 : 0);
    }

    [Fact]
    public void GivenEmptyDescription_WhenRendering_ThenTheRowTakesOneLine()
    {
        var sections = new List<HelpSection> { new("S", [new HelpRow("k", string.Empty)]) };

        Assert.Equal(2, HelpFrameBuilder.ContentHeight(sections, 40));
    }

    [Fact]
    public void GivenLongMessageAtTheLastRow_WhenScrolledToTheEnd_ThenTheTailShows()
    {
        var sections = new List<HelpSection> { new("S", [new HelpRow("e", string.Join(' ', Enumerable.Range(0, 60).Select(index => $"word{index}")))]) };
        var maximum = HelpFrameBuilder.MaximumScroll(sections, 30, 8);

        var frame = HelpFrameBuilder.Build(sections, string.Empty, maximum + 10, 30, 8, GraphGlyphSet.Unicode);

        Assert.True(maximum > 0);
        Assert.Contains("word59", Text(frame), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenPalette_WhenRenderingAt80x24_ThenItListsActionsWithTheirKeys()
    {
        var frame = PaletteFrameBuilder.Build(Palette(), 80, 24, GraphGlyphSet.Unicode);

        Assert.Equal(
            string.Join(
                '\n',
                " : _                                                                    ",
                "│ Jump to HEAD                                                <space>h │",
                "│ Jump to a ref or hash                                       <space>f │",
                "│ Quick jump to a visible row                                 <space>j │",
                "│ Focus the commit details                                       Enter │",
                "│ Copy the commit hash                                               y │",
                "│ Refresh the repository                                      <space>r │",
                "│ Open the commit in the difftool                            <space>td │",
                "│ Resolve conflicts in the mergetool                         <space>tm │",
                "│ Commit the staged changes in $EDITOR                       <space>te │",
                "│ Next bottom pane tab                                               ] │",
                "│ Previous bottom pane tab                                           [ │",
                "│ Show the Details tab                                              gd │",
                "│ 1/135 Ctrl-j/k move  Enter run  Esc close                            │",
                "╰──────────────────────────────────────────────────────────────────────╯"),
            Text(frame));
        Assert.Equal(4, frame!.Column);
    }

    [Fact]
    public void GivenAnUnimplementedAction_WhenRenderingThePalette_ThenItsRowIsFaint()
    {
        var entries = new List<PaletteEntry>
        {
            new(ActionIds.ListDown, "Move down", "j"),
            new(ActionIds.GraphJumpToHead, "Jump to HEAD", "<space>h", false),
        };

        var frame = PaletteFrameBuilder.Build(new PaletteState(entries), 80, 24, GraphGlyphSet.Unicode)!;

        Assert.DoesNotContain(frame.Lines[1].Segments, segment => segment.Role == SegmentRole.Faint);
        Assert.Contains(frame.Lines[2].Segments, segment => segment.Role == SegmentRole.Faint && segment.Text.Contains("Jump to HEAD", StringComparison.Ordinal));
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
                "│ Go to tab 1 (same as 1gt)                        Alt-1 │",
                "│ Go to tab 2 (same as 2gt)                        Alt-2 │",
                "│ Go to tab 3 (same as 3gt)                        Alt-3 │",
                "│ Go to tab 4 (same as 4gt)                        Alt-4 │",
                "│ Go to tab 5 (same as 5gt)                        Alt-5 │",
                "│ 2/34 Ctrl-j/k move  Enter run  Esc close               │",
                "╰────────────────────────────────────────────────────────╯"),
            Text(frame));
        Assert.Equal([false, true, false], [.. frame!.Lines.Skip(1).Take(3).Select(line => line.IsSelected)]);
    }

    [Fact]
    public void GivenHelpForTheGraph_WhenRendering_ThenEveryRowSeparatesKeysFromDescription()
    {
        var frame = HelpFrameBuilder.Build(Help(), DefaultKeymap.GraphView, 0, 80, 24, GraphGlyphSet.Ascii);

        var rows = Text(frame).Split('\n').Skip(1).Where(line => line.StartsWith("|  ", StringComparison.Ordinal) && line.Trim('|', ' ').Length > 0).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Contains(" | ", row, StringComparison.Ordinal));
    }

    [Fact]
    public void GivenThePalette_WhenAskingWhichEntryALineShows_ThenItIsTheEntryDrawnThere()
    {
        var palette = Palette();
        var frame = PaletteFrameBuilder.Build(palette, 80, 24, GraphGlyphSet.Unicode)!;
        var visible = palette.Visible();

        // Line 0 is the query bar, the entries follow, the last two lines are the footer and the bottom border.
        Assert.Null(PaletteFrameBuilder.EntryIndexAt(palette, 0, 24));
        Assert.Equal(0, PaletteFrameBuilder.EntryIndexAt(palette, 1, 24));
        Assert.Equal(3, PaletteFrameBuilder.EntryIndexAt(palette, 4, 24));
        Assert.Null(PaletteFrameBuilder.EntryIndexAt(palette, frame.Height - 2, 24));
        Assert.Contains(visible[3].Title, frame.Lines[4].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAScrolledPalette_WhenAskingWhichEntryALineShows_ThenTheScrollOffsetCounts()
    {
        var palette = Palette().Move(29);
        var frame = PaletteFrameBuilder.Build(palette, 80, 24, GraphGlyphSet.Unicode)!;
        var visible = palette.Visible();

        var index = PaletteFrameBuilder.EntryIndexAt(palette, 2, 24);

        Assert.NotNull(index);
        Assert.True(index > 0);
        Assert.Contains(visible[index.Value].Title, frame.Lines[2].Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(18)]
    [InlineData(24)]
    [InlineData(60)]
    public void GivenAnyScreenHeight_WhenRenderingThePalette_ThenTheFooterIsDirectlyBelowTheLastEntryAndAboveTheStatusRow(int screenHeight)
    {
        var palette = Palette().Move(1000);

        var frame = PaletteFrameBuilder.Build(palette, 80, screenHeight, GraphGlyphSet.Unicode)!;
        var lines = Text(frame).Split('\n');

        Assert.True(frame.Row + lines.Length <= screenHeight - 1, "The frame must end above the status row.");
        var total = PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView).Count;
        Assert.Contains($"{total}/{total}", lines[^2]);
        Assert.True(frame.Lines[^3].IsSelected, "The last entry must be the selected row, directly above the footer.");
    }

    [Fact]
    public void GivenAnOpenPalette_WhenRenderingTheFooter_ThenTheKeysAreBoldInTheStatusBarStyle()
    {
        var frame = PaletteFrameBuilder.Build(Palette(), 80, 24, GraphGlyphSet.Unicode)!;
        var footer = frame.Lines[^2];

        var keys = footer.Segments.Where(segment => segment.Role == SegmentRole.StatusKey).Select(segment => segment.Text);

        Assert.Equal(["Ctrl-j/k", "Enter", "Esc"], keys);
    }

    [Fact]
    public void GivenAFilterWithFewMatches_WhenAskingForALinePastTheEntries_ThenThereIsNoEntry()
    {
        var palette = Palette().Type("zzzzqq");

        Assert.Null(PaletteFrameBuilder.EntryIndexAt(palette, 1, 24));
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
                "| 0/0 Ctrl-j/k move  Enter run  Esc ~|",
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
                "│  <space>h             │ Jump to HEAD                         │",
                "│  <space>f             │ Jump to a ref or hash                │",
                "│  <space>j             │ Quick jump to a visible row          │",
                "│  Enter                │ Focus the commit details             │",
                "│  y                    │ Copy the commit hash                 │",
                "│  <space>r             │ Refresh the repository               │",
                "│  <space>td            │ Open the commit in the difftool      │",
                "│  <space>tm            │ Resolve conflicts in the mergetool   │",
                "│  <space>te            │ Commit the staged changes in $EDITOR │",
                "│  ]                    │ Next bottom pane tab                 │",
                "│  [                    │ Previous bottom pane tab             │",
                "│  gd                   │ Show the Details tab                 │",
                "│  gc                   │ Show the Changes tab                 │",
                "│  gf                   │ Show the Files tab                   │",
                "│  <space>bc            │ Create a branch and check it out     │",
                "│  <space>bC            │ Create a branch without checking out │",
                "│  <space>bs            │ Switch to a branch (check out)       │",
                "│  <space>br            │ Rename the branch                    │",
                "│ 1-19/102  list keys scroll  Esc close                        │",
                "╰──────────────────────────────────────────────────────────────╯"),
            Text(frame));
    }

    [Fact]
    public void GivenHelpScrolledToTheEnd_WhenRenderingAt40x10_ThenTheLastRowsShow()
    {
        var sections = Help();
        var maximum = HelpFrameBuilder.MaximumScroll(sections, 40, 10);

        var frame = HelpFrameBuilder.Build(sections, DefaultKeymap.GraphView, maximum + 50, 40, 10, GraphGlyphSet.Unicode);

        Assert.Contains("Cancel", Text(frame), StringComparison.Ordinal);
        Assert.Equal(10 - 2, frame!.Height);
    }

    [Fact]
    public void GivenWhichKeyAfterG_WhenRenderingAt60x12_ThenThePopupSitsAboveTheStatusBar()
    {
        var engine = new KeymapEngine(Table);
        var state = engine.Process(KeymapState.Initial, new KeyEvent("g"), DefaultKeymap.GraphView).State;
        var rows = WhichKeyBuilder.Build(engine.Continuations(state, DefaultKeymap.GraphView), state.PendingSequence);

        var frame = WhichKeyFrameBuilder.Build("g", rows, 60, 12, GraphGlyphSet.Unicode);

        Assert.Equal(
            string.Join(
                '\n',
                " g-                         ",
                "│ d  Show the Details tab  │",
                "│ c  Show the Changes tab  │",
                "│ f  Show the Files tab    │",
                "│ g  Go to the top         │",
                "│ t  Next tab              │",
                "│ T  Previous tab          │",
                "╰──────────────────────────╯"),
            Text(frame));
        Assert.Equal(60 - frame!.Width, frame.Column);
        Assert.Equal(11 - frame.Height, frame.Row);
    }

    [Fact]
    public void GivenWhichKeyAfterLeader_WhenRenderingAt60x20_ThenGroupRowsNameTheirContents()
    {
        var engine = new KeymapEngine(Table);
        var state = engine.Process(KeymapState.Initial, new KeyEvent(" "), DefaultKeymap.GraphView).State;
        var rows = WhichKeyBuilder.Build(engine.Continuations(state, DefaultKeymap.GraphView), state.PendingSequence);

        var frame = WhichKeyFrameBuilder.Build("<space>", rows, 60, 30, GraphGlyphSet.Unicode);
        var text = Text(frame);

        Assert.Contains("+more (branch operations)", text, StringComparison.Ordinal);
        Assert.Contains("+more (tag operations)", text, StringComparison.Ordinal);
        Assert.All(text.Split('\n'), line => Assert.True(line.Length <= 60));
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
