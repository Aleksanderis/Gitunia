using System.Collections.Immutable;
using Gitunia.Core.Graph;
using Gitunia.Core.Mouse;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Screen;

public class PickerComposerTests
{
    private const int TitleLine = 0;
    private const int QueryRow = 1;
    private const int FirstListLine = 3;

    private static readonly string Home =
        OperatingSystem.IsWindows() ? @"C:\h" : Path.Combine(Path.GetTempPath(), "composer-home");

    private static PickerRow Row(string name, PickerTags tags) => new(Path.Combine(Home, "code", name), name, tags);

    private static PickerState Picker(params PickerRow[] rows) =>
        new(string.Empty, 0, [.. rows], string.Empty, null);

    private static IReadOnlyList<StyledLine> Compose(PickerState picker, int width = 100, int height = 30) =>
        PickerComposer.Compose(picker, Home, width, height, GraphGlyphSet.Unicode);

    [Fact]
    public void GivenAFramedPicker_WhenComposing_ThenTitleWithCountQueryWithPromptAndHintAndBottomRuleAreDrawn()
    {
        var picker = Picker(Row("alpha", PickerTags.Recent)) with { Query = "al" };

        var lines = Compose(picker);

        Assert.Equal(SegmentRole.OverlayTitle, lines[TitleLine].Segments[0].Role);
        Assert.Contains("Open repository", lines[TitleLine].Text, StringComparison.Ordinal);
        Assert.Contains(" al_", lines[QueryRow].Text, StringComparison.Ordinal);
        Assert.Contains(" › al_", lines[QueryRow].Text, StringComparison.Ordinal);
        Assert.Contains(
            lines[QueryRow].Segments,
            segment => segment.Role == ChipStyle.Pane.Key && segment.Text == "Tab");
        Assert.EndsWith("1 repository ", lines[TitleLine].Text, StringComparison.Ordinal);
        Assert.All(lines.Skip(1).Take(lines.Count - 2), line => Assert.Equal(SegmentRole.FocusedBorder, line.Segments[0].Role));
        Assert.All(lines, line => Assert.Equal(lines[0].Text.Length, line.Text.Length));
    }

    [Fact]
    public void GivenRowsOfEverySource_WhenComposing_ThenUppercaseHeadersWithARuleAndSpacersSeparateThem()
    {
        var lines = Compose(Picker(
            Row("alpha", PickerTags.Open | PickerTags.Recent),
            Row("beta", PickerTags.Pinned),
            Row("gamma", PickerTags.Recent),
            Row("delta", PickerTags.Discovered)));

        var headers = lines.Skip(FirstListLine).Where(HasSectionTitle).ToList();
        Assert.Equal(["OPEN", "PINNED", "RECENT", "FOUND"], headers.Select(HeaderName));
        Assert.All(headers, line => Assert.Contains(line.Segments, IsRule));
        Assert.Equal(FirstListLine, lines.ToList().IndexOf(headers[0]));
        foreach (var header in headers.Skip(1))
        {
            var above = lines[lines.ToList().IndexOf(header) - 1];
            Assert.Equal(string.Empty, above.Text.Trim('│', ' '));
        }
    }

    [Fact]
    public void GivenRowsOfEverySource_WhenComposing_ThenEachRowStartsWithItsGlyphInItsRole()
    {
        var lines = Compose(Picker(
            Row("alpha", PickerTags.Open | PickerTags.Recent),
            Row("beta", PickerTags.Pinned),
            Row("gamma", PickerTags.Recent),
            Row("delta", PickerTags.Discovered),
            Row("gone", PickerTags.Recent | PickerTags.Missing)));

        Assert.Equal(("●", SegmentRole.LocalBranch), GlyphOf(lines, "alpha"));
        Assert.Equal(("★", SegmentRole.CurrentBranch), GlyphOf(lines, "beta"));
        Assert.Equal(("◷", SegmentRole.RemoteBranch), GlyphOf(lines, "gamma"));
        Assert.Equal(("○", SegmentRole.Faint), GlyphOf(lines, "delta"));
        Assert.Equal(("!", SegmentRole.FileChanged), GlyphOf(lines, "gone"));
        Assert.DoesNotContain(lines, line => line.Text.Contains("recent ", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenANarrowFramedBox_WhenComposing_ThenTheCountAndTheHintAreDroppedAndTheCloseChipStays()
    {
        var picker = Picker(Row("alpha", PickerTags.Open), Row("beta", PickerTags.Recent));

        var lines = Compose(picker, 32);

        Assert.True(PickerLayout.Compute(picker, 32, 30).IsFramed);
        Assert.DoesNotContain("repositories", lines[TitleLine].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Tab", lines[QueryRow].Text, StringComparison.Ordinal);
        Assert.Contains("Esc", lines[^2].Text, StringComparison.Ordinal);
        Assert.All(lines, line => Assert.Equal(lines[0].Text.Length, line.Text.Length));
    }

    [Fact]
    public void GivenAnOpenAndPinnedRow_WhenComposing_ThenTheOpenGlyphLeadsAndAStarFollowsThePath()
    {
        var lines = Compose(Picker(Row("alpha", PickerTags.Open | PickerTags.Pinned), Row("beta", PickerTags.Open)));

        var starred = Assert.Single(lines, line => line.Text.Contains("alpha", StringComparison.Ordinal));
        var plain = Assert.Single(lines, line => line.Text.Contains("beta", StringComparison.Ordinal));
        Assert.Equal(("●", SegmentRole.LocalBranch), GlyphOf(lines, "alpha"));
        Assert.EndsWith("★ │", starred.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("★", plain.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenRowsWithPaths_WhenComposing_ThenThePathsEndInTheSameColumn()
    {
        var lines = Compose(Picker(
            Row("alpha", PickerTags.Open | PickerTags.Pinned),
            Row("beta", PickerTags.Recent),
            Row("a-much-longer-name", PickerTags.Discovered)));

        var ends = lines
            .Where(line => line.Text.Contains("code", StringComparison.Ordinal))
            .Select(line => line.Text.TrimEnd('│', ' ', '★').Length)
            .Distinct()
            .ToList();
        Assert.Single(ends);
    }

    [Fact]
    public void GivenAFilteredList_WhenComposing_ThenThereAreNoGroupHeaders()
    {
        var picker = Picker(Row("alpha", PickerTags.Open), Row("beta", PickerTags.Recent)) with { Query = "a" };

        var lines = Compose(picker);

        Assert.DoesNotContain(lines.Skip(FirstListLine), HasSectionTitle);
    }

    [Fact]
    public void GivenALongPath_WhenComposing_ThenItIsCutInTheMiddleAndTheTailStaysVisible()
    {
        var path = Path.Combine(Home, "very", "long", "folder", "names", "keep", "going", "deeper", "down", "aa2");
        var picker = Picker(new PickerRow(path, "aa2", PickerTags.Recent));

        var lines = Compose(picker, 56);

        var row = Assert.Single(lines, line => line.Text.Contains("aa2", StringComparison.Ordinal) && line.Text.Contains('…'));
        Assert.Contains("…", row.Text, StringComparison.Ordinal);
        Assert.Contains("aa2", row.Text[(row.Text.IndexOf('…', StringComparison.Ordinal))..], StringComparison.Ordinal);
        Assert.Equal(lines[0].Text.Length, row.Text.Length);
    }

    [Fact]
    public void GivenAMissingRepository_WhenComposing_ThenItIsMarkedInTheWarningRole()
    {
        var lines = Compose(Picker(Row("gone", PickerTags.Open | PickerTags.Missing)));

        Assert.Equal(("!", SegmentRole.FileChanged), GlyphOf(lines, "gone"));
    }

    [Fact]
    public void GivenHighlight_WhenComposing_ThenOnlyThatRowIsSelected()
    {
        var picker = Picker(Row("alpha", PickerTags.Recent), Row("beta", PickerTags.Recent)) with { Highlight = 1 };

        var lines = Compose(picker);

        var selected = lines.Where(line => line.IsSelected).ToList();
        Assert.Single(selected);
        Assert.Contains("beta", selected[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheCaretInTheQuery_WhenComposing_ThenTheQueryLineDrawsItBetweenTheCharacters()
    {
        var picker = new PickerState("abcd", 0, [], string.Empty, null, Caret: 2);

        var lines = Compose(picker);

        Assert.Contains(" ab|cd", lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheCaretAtTheEnd_WhenComposing_ThenTheQueryLineEndsWithTheEndGlyph()
    {
        var lines = Compose(new PickerState("abcd", 0, [], string.Empty, null));

        Assert.Contains(" abcd_", lines[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenMessage_WhenComposing_ThenItIsShownBelowTheListInTheErrorRole()
    {
        var picker = new PickerState("~/x", 0, [], "not a repository: /x", null);

        var lines = Compose(picker);

        var layout = PickerLayout.Compute(picker, 100, 30);
        Assert.Contains("no matching directories", lines[FirstListLine].Text, StringComparison.Ordinal);
        Assert.Contains("not a repository: /x", lines[layout.MessageLine].Text, StringComparison.Ordinal);
        Assert.Contains(lines[layout.MessageLine].Segments, segment => segment.Role == SegmentRole.Error);
    }

    [Fact]
    public void GivenHighlightBelowTheVisibleRows_WhenComposing_ThenTheListScrollsToShowIt()
    {
        var rows = Enumerable.Range(1, 40).Select(number => Row($"repo{number}", PickerTags.Recent)).ToImmutableArray();
        var picker = new PickerState(string.Empty, 30, rows, string.Empty, null);

        var lines = Compose(picker);

        var selected = Assert.Single(lines, line => line.IsSelected);
        Assert.Contains("repo31", selected.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.Text.Contains("repo1 ", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenLocatingMode_WhenComposing_ThenTheTitleSaysLocateAndThereAreNoCloneAndInitChips()
    {
        var picker = new PickerState(string.Empty, 0, [], string.Empty, new TabId(1));

        var lines = Compose(picker);

        Assert.Contains("Locate repository", lines[TitleLine].Text, StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.Text.Contains("Alt-c", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenPendingListing_WhenComposing_ThenScanningIsShown()
    {
        var picker = new PickerState("~/code/", 0, [], string.Empty, null, true, true);

        Assert.Contains("scanning...", Compose(picker)[FirstListLine].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenDirectoryRows_WhenComposing_ThenFoldersEndInTheSeparatorAndRepositoriesDoNot()
    {
        var picker = new PickerState(
            "~/code/",
            0,
            [Row("app", PickerTags.Directory | PickerTags.Repository), Row("docs", PickerTags.Directory)],
            string.Empty,
            null);

        var lines = Compose(picker);

        Assert.Contains("app  ", lines[FirstListLine].Text, StringComparison.Ordinal);
        Assert.Contains($"docs{Path.DirectorySeparatorChar}", lines[FirstListLine + 1].Text, StringComparison.Ordinal);
        Assert.Equal("○", GlyphOf(lines, "app").Text);
        Assert.Equal("○", GlyphOf(lines, "docs").Text);
    }

    [Fact]
    public void GivenAShortList_WhenComposing_ThenTheBoxIsSmallerThanTheArea()
    {
        var picker = Picker(Row("alpha", PickerTags.Recent));

        var layout = PickerLayout.Compute(picker, 120, 40);

        Assert.True(layout.IsFramed);
        Assert.True(layout.BoxHeight < 15);
        Assert.Equal(100, layout.BoxWidth);
        Assert.Equal((120 - 100) / 2, layout.BoxX);
        Assert.Equal(layout.BoxHeight, Compose(picker, 120, 40).Count);
    }

    [Theory]
    [InlineData(80, 24)]
    [InlineData(120, 40)]
    [InlineData(40, 10)]
    [InlineData(20, 5)]
    [InlineData(10, 3)]
    [InlineData(1, 1)]
    public void GivenAnySize_WhenComposing_ThenLinesFitTheAreaWithoutThrowing(int width, int height)
    {
        var rows = Enumerable.Range(1, 30).Select(number => Row($"repo{number}", PickerTags.Recent)).ToImmutableArray();
        var picker = new PickerState("a", 12, rows, "oops", null);

        var lines = Compose(picker, width, height);
        var layout = PickerLayout.Compute(picker, width, height);

        Assert.True(lines.Count <= height);
        Assert.All(lines, line => Assert.True(line.Text.Length <= width));
        Assert.True(layout.BoxX + layout.BoxWidth <= width);
        Assert.True(layout.BoxY + layout.BoxHeight <= height);
    }

    [Fact]
    public void GivenATinyArea_WhenComposing_ThenThePickerIsUnframedButKeepsQueryAndHighlight()
    {
        var picker = Picker(Row("alpha", PickerTags.Recent), Row("beta", PickerTags.Recent)) with { Highlight = 1 };

        var layout = PickerLayout.Compute(picker, 20, 4);
        var lines = Compose(picker, 20, 4);

        Assert.False(layout.IsFramed);
        Assert.DoesNotContain(lines, line => line.Segments.Any(segment => segment.Role == SegmentRole.FocusedBorder));
        Assert.Contains("beta", Assert.Single(lines, line => line.IsSelected).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAsciiGlyphs_WhenComposing_ThenEverythingIsAsciiAndTheChipsAreBracketed()
    {
        var picker = Picker(
            Row("alpha", PickerTags.Open),
            Row("beta", PickerTags.Pinned),
            Row("gamma", PickerTags.Recent),
            Row("delta", PickerTags.Discovered),
            Row("gone", PickerTags.Recent | PickerTags.Missing));

        var lines = PickerComposer.Compose(picker, Home, 100, 30, GraphGlyphSet.Ascii);

        Assert.StartsWith("|", lines[QueryRow].Text, StringComparison.Ordinal);
        Assert.Contains("> ", lines[QueryRow].Text, StringComparison.Ordinal);
        Assert.StartsWith("+", lines[^1].Text, StringComparison.Ordinal);
        Assert.Contains("[", lines[^2].Text, StringComparison.Ordinal);
        Assert.All(lines, line => Assert.True(line.Text.All(char.IsAscii), line.Text));
        Assert.Equal("*", GlyphOf(lines, "alpha").Text);
        Assert.Equal("+", GlyphOf(lines, "beta").Text);
        Assert.Equal("~", GlyphOf(lines, "gamma").Text);
        Assert.Equal("o", GlyphOf(lines, "delta").Text);
        Assert.Equal("!", GlyphOf(lines, "gone").Text);
    }

    [Fact]
    public void GivenAnOpenPicker_WhenComposingTheScreen_ThenTheBoxIsDrawnInsideTheScreen()
    {
        var picker = Picker(Row("alpha", PickerTags.Recent));
        var model = new ScreenModel(string.Empty, null, 0, 0, "INSERT", string.Empty, 0, 0, false, string.Empty, Tabs: [], Picker: picker, HomeDirectory: Home);

        var lines = ScreenComposer.Compose(model, new CommitGraph(), 0, 0, 80, 24, GraphGlyphSet.Unicode);

        Assert.Equal(24, lines.Count);
        Assert.Contains(lines, line => line.Text.Contains("Open repository", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Text.Contains("alpha", StringComparison.Ordinal));
        Assert.Contains(" INSERT ", lines[^1].Text, StringComparison.Ordinal);
        Assert.Contains("1/1", lines[^1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Alt-", lines[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARecentRowHighlighted_WhenLayingOutTheFooter_ThenOpenPinRemoveAreLeftAndCloneInitCloseRight()
    {
        var buttons = PickerFooterButtons.Layout(Row("alpha", PickerTags.Recent), 90, false);

        Assert.Equal(
            [
                DefaultMouseMap.PickerAcceptButton,
                DefaultMouseMap.PickerPinButton,
                DefaultMouseMap.PickerRemoveButton,
                DefaultMouseMap.PickerCloneButton,
                DefaultMouseMap.PickerInitButton,
                DefaultMouseMap.PickerCloseButton,
            ],
            buttons.Select(button => button.View));
        Assert.All(buttons, button => Assert.Equal(button.View, TitleButton.ViewAt(buttons, button.Start)));
        Assert.Null(TitleButton.ViewAt(buttons, buttons[0].Start - 1));
        var last = buttons[^1];
        Assert.Equal(90 - PickerFooterButtons.Margin, last.Start + last.Width);
        Assert.Contains(buttons, button => button.Label == " Enter open ");
        Assert.Contains(buttons, button => button.Label == " Esc close ");
    }

    [Fact]
    public void GivenAPinnedRow_WhenLayingOutTheFooter_ThenItOffersUnpinAndNoRemove()
    {
        var buttons = PickerFooterButtons.Layout(Row("beta", PickerTags.Pinned), 90, false);

        Assert.Contains(buttons, button => button.Label == " Alt-p unpin ");
        Assert.DoesNotContain(buttons, button => button.View == DefaultMouseMap.PickerRemoveButton);
    }

    [Fact]
    public void GivenADirectoryRowOrLocating_WhenLayingOutTheFooter_ThenPinAndCloneAreLeftOut()
    {
        var directory = PickerFooterButtons.Layout(Row("docs", PickerTags.Directory), 90, false);
        var locating = PickerFooterButtons.Layout(Row("alpha", PickerTags.Recent), 90, true);

        Assert.DoesNotContain(directory, button => button.View == DefaultMouseMap.PickerPinButton);
        Assert.DoesNotContain(locating, button => button.View == DefaultMouseMap.PickerCloneButton);
        Assert.Contains(locating, button => button.View == DefaultMouseMap.PickerPinButton);
        Assert.Contains(locating, button => button.View == DefaultMouseMap.PickerCloseButton);
    }

    [Fact]
    public void GivenANarrowFooter_WhenLayingOutTheChips_ThenOnlyTheOnesThatFitAreKeptAndNeverOverlap()
    {
        var wide = PickerFooterButtons.Layout(Row("alpha", PickerTags.Recent), 90, false);
        var narrow = PickerFooterButtons.Layout(Row("alpha", PickerTags.Recent), 40, false);

        Assert.True(narrow.Count < wide.Count);
        Assert.All(narrow, button => Assert.True(button.Start + button.Width <= 40));
        Assert.Contains(narrow, button => button.View == DefaultMouseMap.PickerCloseButton);
        for (var index = 1; index < narrow.Count; index++)
        {
            Assert.True(narrow[index].Start >= narrow[index - 1].Start + narrow[index - 1].Width);
        }
    }

    [Fact]
    public void GivenTheChips_WhenComposing_ThenTheKeyIsDrawnInTheKeyRoleLikeEveryOtherChip()
    {
        var lines = Compose(Picker(Row("alpha", PickerTags.Recent)));

        var footer = lines[^2];
        Assert.Contains(footer.Segments, segment => segment.Text == "Alt-c" && segment.Role == SegmentRole.RepositoryTabKey);
        Assert.DoesNotContain(footer.Segments, segment => segment.Role == SegmentRole.Button);
        Assert.Contains(footer.Segments, segment => IsChipKey(segment, "Enter"));
        Assert.Contains(footer.Segments, segment => IsChipKey(segment, "Esc"));
        Assert.Equal(SegmentRole.Faint, lines[^3].Segments[2].Role);
        Assert.StartsWith("│ ──", lines[^3].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenGroupedRows_WhenAskingForTheRowAtALine_ThenHeadersAndSpacersAreNotRowsAndLinesMapToIndexes()
    {
        var picker = Picker(
            Row("alpha", PickerTags.Open),
            Row("beta", PickerTags.Recent),
            Row("gamma", PickerTags.Recent));

        var layout = PickerLayout.Compute(picker, 100, 30);

        Assert.Null(layout.RowIndexAt(layout.ListFirstLine));
        Assert.Equal(0, layout.RowIndexAt(layout.ListFirstLine + 1));
        Assert.Null(layout.RowIndexAt(layout.ListFirstLine + 2));
        Assert.True(layout.Visible[2].IsSpacer);
        Assert.Null(layout.RowIndexAt(layout.ListFirstLine + 3));
        Assert.Equal(1, layout.RowIndexAt(layout.ListFirstLine + 4));
        Assert.Equal(2, layout.RowIndexAt(layout.ListFirstLine + 5));
    }

    [Fact]
    public void GivenGroupedRowsScrolledToASpacer_WhenComputingTheLayout_ThenTheWindowDoesNotStartOnTheSpacer()
    {
        var rows = Enumerable.Range(1, 12).Select(number => Row($"open{number}", PickerTags.Open))
            .Concat(Enumerable.Range(1, 12).Select(number => Row($"recent{number}", PickerTags.Recent)))
            .ToImmutableArray();
        var picker = new PickerState(string.Empty, 13, rows, string.Empty, null);

        var layout = PickerLayout.Compute(picker, 100, 10);

        Assert.Equal(4, layout.ListHeight);
        Assert.Equal(4, layout.Visible.Count);
        Assert.False(layout.Visible[0].IsSpacer);
        var offset = layout.Visible.ToList().FindIndex(line => line.RowIndex == 13);
        Assert.True(offset >= 0);
        Assert.Equal(13, layout.RowIndexAt(layout.ListFirstLine + offset));
    }

    [Fact]
    public void GivenATinyAreaWithGroups_WhenComputingTheLayout_ThenTheUnframedListHasNoSpacers()
    {
        var picker = Picker(Row("alpha", PickerTags.Open), Row("beta", PickerTags.Recent));

        var layout = PickerLayout.Compute(picker, 20, 8);

        Assert.False(layout.IsFramed);
        Assert.DoesNotContain(layout.Visible, line => line.IsSpacer);
        Assert.Equal(1, layout.RowIndexAt(layout.ListFirstLine + 3));
    }

    [Fact]
    public void GivenAFilteredOrPathList_WhenComputingTheLayout_ThenThereAreNoHeadersOrSpacers()
    {
        var rows = new[] { Row("alpha", PickerTags.Open), Row("beta", PickerTags.Recent) };

        var filtered = PickerLayout.Compute(Picker(rows) with { Query = "a" }, 100, 30);
        var path = PickerLayout.Compute(Picker(rows) with { Query = "~/" }, 100, 30);

        Assert.All(filtered.Visible.Concat(path.Visible), line => Assert.True(line.RowIndex >= 0));
    }

    [Fact]
    public void GivenAMessage_WhenComposing_ThenTheFooterIsTheRowAboveTheBottomRuleAndTheBoxMatchesTheLines()
    {
        var picker = Picker(Row("alpha", PickerTags.Open), Row("beta", PickerTags.Recent)) with { Message = "oops" };

        var layout = PickerLayout.Compute(picker, 100, 30);
        var lines = Compose(picker);

        Assert.Equal(layout.BoxHeight, lines.Count);
        Assert.True(layout.IsFooterLine(lines.Count - 2));
        Assert.Contains("Esc", lines[layout.BoxHeight - 2].Text, StringComparison.Ordinal);
        Assert.Contains("oops", lines[layout.MessageLine].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAScrolledList_WhenAskingForTheRowAtALine_ThenLinesMapToTheShownRows()
    {
        var rows = Enumerable.Range(1, 40).Select(number => Row($"repo{number}", PickerTags.Recent)).ToImmutableArray();
        var picker = new PickerState(string.Empty, 30, rows, string.Empty, null);

        var layout = PickerLayout.Compute(picker, 100, 30);

        Assert.Equal(30, layout.RowIndexAt(layout.ListFirstLine + layout.ListHeight - 1));
        Assert.Equal(30 - layout.ListHeight + 1, layout.RowIndexAt(layout.ListFirstLine));
        Assert.Null(layout.RowIndexAt(layout.ListFirstLine + layout.ListHeight));
    }

    private static Dictionary<string, string> Branches(params (string Name, string Branch)[] pairs) =>
        pairs.ToDictionary(pair => Row(pair.Name, PickerTags.Open).Path, pair => pair.Branch);

    private static IReadOnlyList<StyledLine> ComposeWithBranches(
        PickerState picker,
        IReadOnlyDictionary<string, string> branches,
        int width) =>
        PickerComposer.Compose(picker, Home, width, 30, GraphGlyphSet.Unicode, null, branches);

    [Fact]
    public void GivenTwoOpenTabsWithBranches_WhenComposing_ThenEachOpenRowShowsItsBranchInCyanBetweenNameAndPath()
    {
        var picker = Picker(
            Row("alpha", PickerTags.Open),
            Row("beta", PickerTags.Open),
            Row("gamma", PickerTags.Recent),
            Row("delta", PickerTags.Pinned),
            Row("omega", PickerTags.Discovered));

        var all = Branches(("alpha", "main"), ("beta", "fix/9"), ("gamma", "recent"), ("delta", "pinned"), ("omega", "found"));
        var lines = ComposeWithBranches(picker, all, 100);
        var plain = Compose(picker);

        var alpha = lines.Single(line => line.Text.Contains("alpha", StringComparison.Ordinal));
        Assert.Contains(alpha.Segments, segment => segment.Role == SegmentRole.CurrentBranch && segment.Text == "main");
        var path = "~/code/alpha";
        Assert.True(alpha.Text.IndexOf("alpha", StringComparison.Ordinal) < alpha.Text.IndexOf("main", StringComparison.Ordinal));
        Assert.True(alpha.Text.IndexOf("main", StringComparison.Ordinal) < alpha.Text.IndexOf(path, StringComparison.Ordinal));
        var beta = lines.Single(line => line.Text.Contains("beta", StringComparison.Ordinal));
        Assert.Contains(beta.Segments, segment => segment.Role == SegmentRole.CurrentBranch && segment.Text == "fix/9");
        foreach (var name in new[] { "gamma", "delta", "omega" })
        {
            var line = lines.Single(candidate => candidate.Text.Contains(name, StringComparison.Ordinal));
            Assert.DoesNotContain(all[Row(name, PickerTags.Open).Path], line.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(line.Segments, segment => segment.Text.Contains("recent", StringComparison.Ordinal)
                || segment.Text.Contains("pinned", StringComparison.Ordinal) || segment.Text.Contains("found", StringComparison.Ordinal));
        }

        Assert.Equal(plain.Count, lines.Count);
    }

    [Fact]
    public void GivenAnOpenTabWithoutABranchOrMissing_WhenComposing_ThenNoBranchColumnIsDrawn()
    {
        var picker = Picker(Row("alpha", PickerTags.Open), Row("beta", PickerTags.Open | PickerTags.Missing));

        var detached = ComposeWithBranches(picker, Branches(), 100);
        var missing = ComposeWithBranches(picker, Branches(("beta", "main")), 100);

        Assert.All(detached.Concat(missing), line => Assert.DoesNotContain("main", line.Text, StringComparison.Ordinal));
        Assert.Equal(Compose(picker).Select(line => line.Text), detached.Select(line => line.Text));
    }

    [Fact]
    public void GivenANarrowBox_WhenComposingWithBranches_ThenTheBranchColumnIsDroppedAndThePathStaysWhole()
    {
        var picker = Picker(Row("alpha", PickerTags.Open));
        var branches = Branches(("alpha", "feature/long-branch"));

        var narrow = ComposeWithBranches(picker, branches, 44);
        var wide = ComposeWithBranches(picker, branches, 100);

        var narrowRow = narrow.Single(line => line.Text.Contains("alpha", StringComparison.Ordinal));
        Assert.DoesNotContain("feature", narrowRow.Text, StringComparison.Ordinal);
        Assert.Contains("~/code/alpha", narrowRow.Text, StringComparison.Ordinal);
        Assert.Contains("feature/long-branch", wide.Single(line => line.Text.Contains("alpha", StringComparison.Ordinal)).Text, StringComparison.Ordinal);
    }

    private static bool HasSectionTitle(StyledLine line) =>
        line.Segments.Any(segment => segment.Role == SegmentRole.SectionTitle);

    private static bool IsRule(StyledSegment segment) =>
        segment.Role == SegmentRole.Faint && segment.Text.StartsWith('─');

    private static bool IsChipKey(StyledSegment segment, string key) =>
        segment.Text == key && segment.Role == SegmentRole.RepositoryTabKey;

    private static string HeaderName(StyledLine line) =>
        line.Segments.Single(segment => segment.Role == SegmentRole.SectionTitle).Text.Trim();

    private static (string Text, SegmentRole Role) GlyphOf(IReadOnlyList<StyledLine> lines, string name)
    {
        var line = lines.Single(candidate => candidate.Text.Contains(name, StringComparison.Ordinal));
        var segment = line.Segments[2];
        return (segment.Text, segment.Role);
    }
}
