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

    private static bool IsRule(string line, GraphGlyphSet glyphs)
    {
        var inner = line.Trim('|', '\u2502', ' ');

        return inner.Length > 0 && inner.All(character => character == glyphs.Horizontal);
    }

    private static PaletteState Palette() => new(PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView));

    private static IReadOnlyList<HelpTab> Page(List<HelpSection> sections) => [new HelpTab(string.Empty, sections)];

    private static IReadOnlyList<HelpTab> Help() => HelpBuilder.Build(Table, DefaultKeymap.GraphView);

    private static ConfirmPrompt DiscardPrompt() => new(
        "Discard changes",
        ["Discard all unstaged changes to src/Gitunia.Tui/App/WorkingTree/DiscardActions.cs?", "Its staged changes are kept."],
        "The changes are removed from the file on disk. Nothing keeps a copy: this cannot be undone.",
        "Discard changes");

    [Fact]
    public void GivenConfirmDialog_WhenRenderingAt66x24_ThenItIsCentredAndNamesWhatIsLost()
    {
        var state = OverlayState.OpenConfirm(DiscardPrompt(), KeyMode.Normal);

        var frame = OverlayFrameFactory.Create(state, [], string.Empty, [], 66, 24, GraphGlyphSet.Unicode);

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
        Assert.Equal(1, frame!.Column);
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

        var frame = OverlayFrameFactory.Create(state, Page(sections), string.Empty, [], 60, 10, GraphGlyphSet.Unicode);

        var text = Text(frame);
        Assert.Contains("Status messages", text, StringComparison.Ordinal);
        Assert.Contains("error │ repo: could not read the stashes", text, StringComparison.Ordinal);
        Assert.Contains("Esc close", text, StringComparison.Ordinal);
        var tiny = OverlayFrameFactory.Create(state, Page(sections), string.Empty, [], 20, 6, GraphGlyphSet.Unicode);
        Assert.NotNull(tiny);
    }

    [Fact]
    public void GivenOutputOverlay_WhenRendering_ThenItShowsItsTitleAndTheOutputLines()
    {
        var sections = new List<HelpSection> { new("Output of git commit", [new HelpRow(string.Empty, "pre-commit: forbidden word"), new HelpRow(string.Empty, "  indented")]) };
        var state = OverlayState.OpenOutput("Commit failed", KeyMode.Insert);

        var frame = OverlayFrameFactory.Create(state, Page(sections), string.Empty, [], 70, 12, GraphGlyphSet.Unicode);

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

        var frame = OverlayFrameFactory.Create(state, Page(sections), string.Empty, [], 40, 20, GraphGlyphSet.Unicode);

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
                " : _                                                                          ",
                "│ Branch                                                                     │",
                "│ Create a branch and check it out                                 <space>bc │",
                "│ Create a branch without checking out                             <space>bC │",
                "│ Switch to a branch (check out)                                   <space>bs │",
                "│ Rename the branch                                                <space>br │",
                "│ Delete the branch (asks first)                                   <space>bd │",
                "│ Set the branch upstream                                          <space>bu │",
                "│ Unset the branch upstream                                        <space>bU │",
                "│ Fast-forward the branch to its upstream (no checkout)            <space>bf │",
                "│                                                                            │",
                "│ Merge and rebase                                                           │",
                "│ Reset the current branch to the selection                        <space>gx │",
                "│ 1/176 Ctrl-j/k move  Enter run  Esc close                                  │",
                "╰────────────────────────────────────────────────────────────────────────────╯"),
            Text(frame));
        Assert.Equal(1, frame!.Column);
    }

    [Fact]
    public void GivenGroupedEntries_WhenRenderingAtTinyAsciiSize_ThenSubheadersShowAndFitTheWidth()
    {
        var frame = PaletteFrameBuilder.Build(Palette(), 40, 8, GraphGlyphSet.Ascii)!;

        Assert.Equal(
            string.Join(
                '\n',
                " : _                                  ",
                "| Branch                             |",
                "| Create a branch and ch~  <space>bc |",
                "| Create a branch withou~  <space>bC |",
                "| 1/176 Ctrl-j/k move  Enter run  Esc|",
                "+------------------------------------+"),
            Text(frame));
        Assert.Equal(SegmentRole.SectionTitle, frame.Lines[1].Segments[1].Role);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    public void GivenAScrollingList_WhenRowsReachTheThreshold_ThenGapsAppearOnlyFromEightRows(int rows, bool gaps)
    {
        var entries = Palette().Visible();

        var grouped = GroupedRows.Of(entries, rows, scrolls: true);

        Assert.Equal(gaps, grouped.Lines.Any(line => line is { Header: null, Entry: null }));
        Assert.False(grouped.Lines[0] is { Header: null, Entry: null });
    }

    [Fact]
    public void GivenAWindowStartingOnAGap_WhenScrolling_ThenTheFirstRowIsNotBlank()
    {
        var grouped = GroupedRows.Of(Palette().Visible(), 12, scrolls: true);
        var gap = grouped.Lines.ToList().FindIndex(line => line is { Header: null, Entry: null });

        for (var selected = 0; selected < 30; selected++)
        {
            var window = grouped.Window(selected, 12);
            Assert.False(grouped.Lines[window.Top] is { Header: null, Entry: null }, $"selected {selected}, gap {gap}");
        }
    }

    [Fact]
    public void GivenGaps_WhenHitTestingThePalette_ThenGapAndSubheaderLinesAreNoEntry()
    {
        var palette = Palette();
        var hits = Enumerable.Range(0, 14).Select(line => PaletteFrameBuilder.EntryIndexAt(palette, line, 24)).ToList();

        Assert.Null(hits[1]);
        Assert.Equal(6, hits[8]);
        Assert.Equal(7, hits[9]);
        Assert.Null(hits[10]);
        Assert.Null(hits[11]);
        Assert.Equal(0, hits[2]);
    }

    [Fact]
    public void GivenAFilterMatchingOneGroup_WhenRenderingThePalette_ThenOtherGroupsAreHidden()
    {
        var frame = PaletteFrameBuilder.Build(Palette().Type("stash"), 80, 24, GraphGlyphSet.Unicode)!;
        var text = Text(frame);

        Assert.Contains("Stash", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Tabs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Navigate", text, StringComparison.Ordinal);
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
                "│ Tabs                                                   │",
                "│ Next tab                                            gt │",
                "│ Previous tab                                        gT │",
                "│ Go to tab 1 (same as 1gt)                        Alt-1 │",
                "│ Go to tab 2 (same as 2gt)                        Alt-2 │",
                "│ Go to tab 3 (same as 3gt)                        Alt-3 │",
                "│ Go to tab 4 (same as 4gt)                        Alt-4 │",
                "│ 2/38 Ctrl-j/k move  Enter run  Esc close               │",
                "╰────────────────────────────────────────────────────────╯"),
            Text(frame));
        Assert.Equal([false, false, true, false], [.. frame!.Lines.Skip(1).Take(4).Select(line => line.IsSelected)]);
    }

    [Fact]
    public void GivenHelpForTheGraph_WhenRendering_ThenEveryRowSeparatesKeysFromDescription()
    {
        var frame = HelpFrameBuilder.Build(Help(), 0, DefaultKeymap.GraphView, 0, 80, 24, GraphGlyphSet.Ascii);

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
        Assert.Null(PaletteFrameBuilder.EntryIndexAt(palette, 1, 24));
        Assert.Equal(0, PaletteFrameBuilder.EntryIndexAt(palette, 2, 24));
        Assert.Equal(2, PaletteFrameBuilder.EntryIndexAt(palette, 4, 24));
        Assert.Null(PaletteFrameBuilder.EntryIndexAt(palette, frame.Height - 2, 24));
        Assert.Contains(visible[2].Title, frame.Lines[4].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAScrolledPalette_WhenAskingWhichEntryALineShows_ThenTheScrollOffsetCounts()
    {
        var palette = Palette().Move(31);
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
    public void GivenHelpForTheGraph_WhenRenderingAt66x24_ThenItShowsTheTabRowAndTheFirstTabUnderGroupSubheaders()
    {
        var frame = HelpFrameBuilder.Build(Help(), 0, DefaultKeymap.GraphView, 0, 66, 24, GraphGlyphSet.Unicode);

        Assert.Equal(
            string.Join(
                '\n',
                " Keys: Graph                                                    ",
                "│ [Graph]  Lists   Global   Markers                            │",
                "│ Branch                                                       │",
                "│  <space>bc            │ Create a branch and check it out     │",
                "│  <space>bC            │ Create a branch without checking out │",
                "│  <space>bs            │ Switch to a branch (check out)       │",
                "│  <space>br            │ Rename the branch                    │",
                "│  <space>bd            │ Delete the branch (asks first)       │",
                "│  <space>bu            │ Set the branch upstream              │",
                "│  <space>bU            │ Unset the branch upstream            │",
                "│  <space>bf            │ Fast-forward the branch to its       │",
                "│                       │ upstream (no checkout)               │",
                "│                                                              │",
                "│ Merge and rebase                                             │",
                "│  <space>gx            │ Reset the current branch to the      │",
                "│                       │ selection                            │",
                "│  <space>gr            │ Rebase the current branch onto the   │",
                "│                       │ selection                            │",
                "│  <space>gi            │ Interactive rebase onto the selection│",
                "│                       │ (edit the todo)                      │",
                "│ 1-18/71  Esc close  Tab next tab  list keys scroll           │",
                "╰──────────────────────────────────────────────────────────────╯"),
            Text(frame));
    }

    [Fact]
    public void GivenHelpScrolledToTheEnd_WhenRenderingAt40x10_ThenTheLastRowsShow()
    {
        var sections = Help();
        var maximum = HelpFrameBuilder.MaximumScroll(sections, 3, 40, 10);

        var frame = HelpFrameBuilder.Build(
            sections,
            3,
            DefaultKeymap.GraphView,
            maximum + 50,
            40,
            10,
            GraphGlyphSet.Unicode);

        Assert.Contains("pruned", Text(frame), StringComparison.Ordinal);
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
                "│ c  Show the Changes tab  │",
                "│ d  Show the Details tab  │",
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
    public void GivenWhichKeyAfterLeader_WhenRenderingTheTitle_ThenAThinSpaceSeparatesTheAngleBracketFromTheDash()
    {
        var engine = new KeymapEngine(Table);
        var state = engine.Process(KeymapState.Initial, new KeyEvent(" "), DefaultKeymap.GraphView).State;
        var rows = WhichKeyBuilder.Build(engine.Continuations(state, DefaultKeymap.GraphView), state.PendingSequence);

        var frame = WhichKeyFrameBuilder.Build("<space>", rows, 60, 30, GraphGlyphSet.Unicode)!;

        Assert.Contains("<space>\u2009-", frame.Lines[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain(">-", frame.Lines[0].Text, StringComparison.Ordinal);
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
    [InlineData(true)]
    [InlineData(false)]
    public void GivenTheLeaderPopup_WhenRendering_ThenARuleSeparatesTheActionsFromTheMoreGroups(bool unicode)
    {
        var engine = new KeymapEngine(Table);
        var state = engine.Process(KeymapState.Initial, new KeyEvent(" "), DefaultKeymap.GraphView).State;
        var rows = WhichKeyBuilder.Build(engine.Continuations(state, DefaultKeymap.GraphView), state.PendingSequence);
        var glyphs = GraphGlyphSet.For(unicode);

        var frame = WhichKeyFrameBuilder.Build("<space>", rows, 80, 40, glyphs)!;
        var body = frame.Lines.Skip(1).Take(frame.Height - 2).Select(line => line.Text).ToList();
        var ruleIndex = body.FindIndex(line => IsRule(line, glyphs));

        Assert.True(ruleIndex > 0);
        Assert.DoesNotContain("+more", body[ruleIndex - 1], StringComparison.Ordinal);
        Assert.Contains("+more", body[ruleIndex + 1], StringComparison.Ordinal);
        Assert.All(frame.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenEveryPaletteEntry_WhenRenderingAt120Columns_ThenItsRowIsNotCut()
    {
        var palette = Palette();

        for (var index = 0; index < palette.Entries.Count; index++)
        {
            var scrolled = palette with { SelectedIndex = index };
            var frame = PaletteFrameBuilder.Build(scrolled, 120, 30, GraphGlyphSet.Unicode)!;

            Assert.Contains(palette.Entries[index].Title, Text(frame), StringComparison.Ordinal);
            Assert.DoesNotContain(frame.Lines, line => line.Text.Contains('\u2026', StringComparison.Ordinal));
        }
    }

    [Fact]
    public void GivenAPopupWithOnlyActions_WhenRendering_ThenThereIsNoRule()
    {
        WhichKeyRow[] rows = [new("g", "Go to the top")];

        var frame = WhichKeyFrameBuilder.Build("g", rows, 60, 20, GraphGlyphSet.Unicode)!;

        Assert.Equal(3, frame.Height);
    }

    [Fact]
    public void GivenARoomForOneRowOnly_WhenRenderingMixedRows_ThenNoRuleIsDrawnAndBothRowsShow()
    {
        WhichKeyRow[] rows = [new("a", "Alpha"), new("b", "+more")];

        var frame = WhichKeyFrameBuilder.Build("<space>", rows, 60, 6, GraphGlyphSet.Unicode)!;

        Assert.Contains("Alpha", Text(frame), StringComparison.Ordinal);
        Assert.Contains("+more", Text(frame), StringComparison.Ordinal);
        Assert.True(frame.Row + frame.Height <= 6);
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
            HelpFrameBuilder.Build(Help(), 0, "Graph", 0, width, height, GraphGlyphSet.Unicode),
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

    [Theory]
    [InlineData(80)]
    [InlineData(100)]
    [InlineData(120)]
    public void GivenGraphHelp_WhenRenderingEachTab_ThenTheTabRowNamesAllFourAndMarksTheActiveOne(int width)
    {
        var tabs = Help();

        for (var tab = 0; tab < tabs.Count; tab++)
        {
            var frame = HelpFrameBuilder.Build(tabs, tab, DefaultKeymap.GraphView, 0, width, 30, GraphGlyphSet.Ascii);
            var row = frame!.Lines[1].Text;

            Assert.Contains($"[{tabs[tab].Title}]", row, StringComparison.Ordinal);
            Assert.All(tabs, other => Assert.Contains(other.Title, row, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void GivenNarrowScreen_WhenRenderingHelp_ThenTheTabRowCollapsesToTheActiveLabelWithItsPosition()
    {
        var frame = HelpFrameBuilder.Build(Help(), 1, DefaultKeymap.GraphView, 0, 30, 20, GraphGlyphSet.Ascii)!;

        Assert.Contains("[Lists] 2/4", frame.Lines[1].Text, StringComparison.Ordinal);
        Assert.Equal(2, HelpFrameBuilder.TabAt(Help(), 1, 30, 1, 3));
    }

    [Fact]
    public void GivenTheTabRow_WhenAskingWhatALabelColumnHits_ThenItIsThatTabAndNothingElseIs()
    {
        var tabs = Help();

        Assert.Equal(0, HelpFrameBuilder.TabAt(tabs, 0, 80, 1, 3));
        Assert.Equal(1, HelpFrameBuilder.TabAt(tabs, 0, 80, 1, 12));
        Assert.Null(HelpFrameBuilder.TabAt(tabs, 0, 80, 2, 3));
        Assert.Null(HelpFrameBuilder.TabAt(tabs, 0, 80, 1, 70));
    }

    [Fact]
    public void GivenGraphHelpOnANarrowScreen_WhenRendering_ThenTheFooterStillSaysHowToClose()
    {
        var frame = HelpFrameBuilder.Build(Help(), 0, DefaultKeymap.GraphView, 0, 44, 20, GraphGlyphSet.Ascii)!;

        Assert.Contains("Esc close", frame.Lines[^2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenSinglePage_WhenRenderingHelp_ThenThereIsNoTabRow()
    {
        var sections = new List<HelpSection> { new("S", [new HelpRow("k", "does a thing")]) };

        var frame = HelpFrameBuilder.Build(sections, string.Empty, 0, 60, 12, GraphGlyphSet.Ascii)!;

        Assert.DoesNotContain("[", frame.Lines[1].Text, StringComparison.Ordinal);
        Assert.Null(HelpFrameBuilder.TabAt([new HelpTab("S", [])], 0, 60, 1, 3));
    }

    [Fact]
    public void GivenAWideScreen_WhenRenderingHelp_ThenItIsWiderThanTheOldLimit()
    {
        var frame = HelpFrameBuilder.Build(Help(), 0, DefaultKeymap.GraphView, 0, 120, 30, GraphGlyphSet.Unicode)!;

        Assert.Equal(96, frame.Width);
        Assert.All(frame.Lines, line => Assert.Equal(96, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenAWideScreen_WhenRenderingConfirm_ThenALongLineIsNotWrapped()
    {
        var state = OverlayState.OpenConfirm(DiscardPrompt(), KeyMode.Normal);

        var frame = OverlayFrameFactory.Create(state, [], string.Empty, [], 120, 30, GraphGlyphSet.Unicode)!;

        Assert.Contains(frame.Lines, line => line.Text.Contains(DiscardPrompt().Lines[0], StringComparison.Ordinal));
        Assert.Contains(frame.Lines, line => line.Text.Contains(DiscardPrompt().Consequence, StringComparison.Ordinal));
        Assert.True(frame.Width > 64 && frame.Width <= 96);
    }

    [Fact]
    public void GivenAWideScreen_WhenRenderingAShortConfirm_ThenItKeepsTheCompactWidth()
    {
        var prompt = new ConfirmPrompt("Delete", ["Delete branch x?"], "Cannot be undone.", "Delete");
        var state = OverlayState.OpenConfirm(prompt, KeyMode.Normal);

        var frame = OverlayFrameFactory.Create(state, [], string.Empty, [], 120, 30, GraphGlyphSet.Unicode)!;

        Assert.Equal(64, frame.Width);
    }

    [Fact]
    public void GivenAWideScreen_WhenRenderingWhichKey_ThenALongDescriptionIsNotCut()
    {
        var description = "Toggle the name-first or the full file paths in every changed files list";

        var rows = new[] { new WhichKeyRow("n", description) };

        var frame = WhichKeyFrameBuilder.Build("<space>v", rows, 120, 30, GraphGlyphSet.Unicode)!;

        Assert.Contains(frame.Lines, line => line.Text.Contains(description, StringComparison.Ordinal));
        Assert.True(frame.Width > 56);
    }
}
