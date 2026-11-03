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
    private static readonly string[] Kinds = ["open", "pinned", "recent", "found"];

    private const int TitleLine = 0;
    private const int QueryRow = 1;
    private const int HintLine = 2;
    private const int FirstListLine = 3;

    private static readonly string Home =
        OperatingSystem.IsWindows() ? @"C:\h" : Path.Combine(Path.GetTempPath(), "composer-home");

    private static PickerRow Row(string name, PickerTags tags) => new(Path.Combine(Home, "code", name), name, tags);

    private static PickerState Picker(params PickerRow[] rows) =>
        new(string.Empty, 0, [.. rows], string.Empty, null);

    private static IReadOnlyList<StyledLine> Compose(PickerState picker, int width = 100, int height = 30) =>
        PickerComposer.Compose(picker, Home, width, height, GraphGlyphSet.Unicode);

    [Fact]
    public void GivenAFramedPicker_WhenComposing_ThenTitleChipQueryHintAndBottomRuleAreDrawn()
    {
        var picker = Picker(Row("alpha", PickerTags.Recent)) with { Query = "al" };

        var lines = Compose(picker);

        Assert.Equal(SegmentRole.OverlayTitle, lines[TitleLine].Segments[0].Role);
        Assert.Contains("Open repository", lines[TitleLine].Text, StringComparison.Ordinal);
        Assert.Contains(" al_", lines[QueryRow].Text, StringComparison.Ordinal);
        Assert.Contains(lines[HintLine].Segments, segment => segment.Role == SegmentRole.OverlayKey && segment.Text == "Tab");
        Assert.All(lines.Skip(1).Take(lines.Count - 2), line => Assert.Equal(SegmentRole.FocusedBorder, line.Segments[0].Role));
        Assert.All(lines, line => Assert.Equal(lines[0].Text.Length, line.Text.Length));
    }

    [Fact]
    public void GivenRowsOfEverySource_WhenComposing_ThenGroupHeadersSeparateThemAndKindsHaveTheirOwnRole()
    {
        var lines = Compose(Picker(
            Row("alpha", PickerTags.Open | PickerTags.Recent),
            Row("beta", PickerTags.Pinned),
            Row("gamma", PickerTags.Recent),
            Row("delta", PickerTags.Discovered)));

        var headers = lines.Where(line => line.Segments.Any(segment => segment.Role == SegmentRole.SectionTitle))
            .Select(line => line.Text.Trim('│', ' '));
        Assert.Equal(["Open", "Pinned", "Recent", "Found"], headers);
        var roles = Kinds
            .Select(kind => lines.SelectMany(line => line.Segments).First(segment => segment.Text == kind).Role)
            .ToList();
        Assert.Equal(4, roles.Distinct().Count());
    }

    [Fact]
    public void GivenAFilteredList_WhenComposing_ThenThereAreNoGroupHeaders()
    {
        var picker = Picker(Row("alpha", PickerTags.Open), Row("beta", PickerTags.Recent)) with { Query = "a" };

        var lines = Compose(picker);

        Assert.DoesNotContain(lines, line => line.Segments.Any(segment => segment.Role == SegmentRole.SectionTitle));
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

        var tag = lines.SelectMany(line => line.Segments).Single(segment => segment.Text == "missing");
        Assert.Equal(SegmentRole.FileChanged, tag.Role);
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
    public void GivenDirectoryRows_WhenComposing_ThenRepositoriesAreMarked()
    {
        var picker = new PickerState(
            "~/code/",
            0,
            [Row("app", PickerTags.Directory | PickerTags.Repository), Row("docs", PickerTags.Directory)],
            string.Empty,
            null);

        var lines = Compose(picker);

        Assert.Contains("repository", lines[FirstListLine].Text, StringComparison.Ordinal);
        Assert.Contains("dir", lines[FirstListLine + 1].Text, StringComparison.Ordinal);
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
    public void GivenAsciiGlyphs_WhenComposing_ThenTheFrameUsesAsciiAndTheChipsAreBracketed()
    {
        var picker = Picker(Row("alpha", PickerTags.Recent));

        var lines = PickerComposer.Compose(picker, Home, 100, 30, GraphGlyphSet.Ascii);

        Assert.StartsWith("|", lines[QueryRow].Text, StringComparison.Ordinal);
        Assert.StartsWith("+", lines[^1].Text, StringComparison.Ordinal);
        Assert.Contains("[", lines[^2].Text, StringComparison.Ordinal);
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
        Assert.Contains("Alt-p pin", lines[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARecentRowHighlighted_WhenLayingOutTheFooter_ThenCloneInitPinAndRemoveAreInOrderAndHit()
    {
        var buttons = PickerFooterButtons.Layout(Row("alpha", PickerTags.Recent), 90, false);

        Assert.Equal(
            [
                DefaultMouseMap.PickerCloneButton,
                DefaultMouseMap.PickerInitButton,
                DefaultMouseMap.PickerPinButton,
                DefaultMouseMap.PickerRemoveButton,
            ],
            buttons.Select(button => button.View));
        Assert.All(buttons, button => Assert.Equal(button.View, TitleButton.ViewAt(buttons, button.Start)));
        Assert.Null(TitleButton.ViewAt(buttons, buttons[0].Start - 1));
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
    }

    [Fact]
    public void GivenANarrowFooter_WhenLayingOutTheChips_ThenOnlyTheOnesThatFitAreKept()
    {
        var wide = PickerFooterButtons.Layout(Row("alpha", PickerTags.Recent), 90, false);
        var narrow = PickerFooterButtons.Layout(Row("alpha", PickerTags.Recent), 30, false);

        Assert.True(narrow.Count < wide.Count);
        Assert.All(narrow, button => Assert.True(button.Start + button.Width <= 30));
    }

    [Fact]
    public void GivenTheChips_WhenComposing_ThenTheKeyIsDrawnInTheKeyRoleLikeEveryOtherChip()
    {
        var lines = Compose(Picker(Row("alpha", PickerTags.Recent)));

        var footer = lines[^2];
        Assert.Contains(footer.Segments, segment => segment.Text == "Alt-c" && segment.Role == SegmentRole.RepositoryTabKey);
        Assert.DoesNotContain(footer.Segments, segment => segment.Role == SegmentRole.Button);
    }

    [Fact]
    public void GivenGroupedRows_WhenAskingForTheRowAtALine_ThenHeadersAreNotRowsAndLinesMapToIndexes()
    {
        var picker = Picker(Row("alpha", PickerTags.Open), Row("beta", PickerTags.Recent));

        var layout = PickerLayout.Compute(picker, 100, 30);

        Assert.Null(layout.RowIndexAt(layout.ListFirstLine));
        Assert.Equal(0, layout.RowIndexAt(layout.ListFirstLine + 1));
        Assert.Null(layout.RowIndexAt(layout.ListFirstLine + 2));
        Assert.Equal(1, layout.RowIndexAt(layout.ListFirstLine + 3));
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
}
