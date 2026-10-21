using Gitunia.Core.Diff;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Diff;

public class DiffTitleButtonsTests
{
    [Fact]
    public void GivenAWideTitleRow_WhenLayingOut_ThenEveryButtonIsShownInKeyOrderAtTheRightEdge()
    {
        var buttons = DiffTitleButtons.Layout(100);

        Assert.Equal([" ‹ hunk ", " hunk › ", " ctx- ", " ctx+ ", " file ", " -w ", " split ", " wrap ", " zoom "], buttons.Select(button => button.Label));
        Assert.Equal(98, buttons[^1].Start + buttons[^1].Width);
        Assert.Equal(1, buttons[1].Start - (buttons[0].Start + buttons[0].Width));
    }

    [Fact]
    public void GivenANarrowTitleRow_WhenLayingOut_ThenOnlyTheMostUsefulButtonsRemainAndTheTitleKeepsItsWidth()
    {
        var buttons = DiffTitleButtons.Layout(50);

        Assert.Equal([" wrap ", " zoom "], buttons.Select(button => button.Label));
        Assert.True(buttons[0].Start >= DiffTitleButtons.MinimumTitleWidth);
    }

    [Fact]
    public void GivenATypicalPaneWidth_WhenLayingOut_ThenTheHunkButtonsStayAheadOfTheOptionButtons()
    {
        var buttons = DiffTitleButtons.Layout(80);

        Assert.Equal([" ‹ hunk ", " hunk › ", " -w ", " split ", " wrap ", " zoom "], buttons.Select(button => button.Label));
    }

    [Fact]
    public void GivenTheUnstagedSide_WhenLayingOut_ThenStageAndDiscardLeadTheRow()
    {
        var buttons = DiffTitleButtons.Layout(160, WorkingTreeButtons.Unstaged);

        Assert.Equal(
            [" stage ", " discard ", " ‹ hunk ", " hunk › ", " ctx- ", " ctx+ ", " file ", " -w ", " split ", " wrap ", " zoom "],
            buttons.Select(button => button.Label));
    }

    [Fact]
    public void GivenTheStagedSide_WhenLayingOut_ThenUnstageIsThereAndDiscardIsNot()
    {
        var labels = DiffTitleButtons.Layout(160, WorkingTreeButtons.Staged).Select(button => button.Label).ToList();

        Assert.Equal(" unstage ", labels[0]);
        Assert.DoesNotContain(" discard ", labels);
        Assert.DoesNotContain(" stage ", labels);
    }

    [Fact]
    public void GivenANarrowRowOnTheUnstagedSide_WhenLayingOut_ThenStageSurvivesBeforeZoomAndWrap()
    {
        var labels = DiffTitleButtons.Layout(62, WorkingTreeButtons.Unstaged).Select(button => button.Label).ToList();

        Assert.Equal([" stage ", " hunk › ", " wrap ", " zoom "], labels);
    }

    [Fact]
    public void GivenTheHistoryDiff_WhenLayingOut_ThenNoStagingButtonShows()
    {
        var views = DiffTitleButtons.Layout(160).Select(button => button.View).ToList();

        Assert.DoesNotContain(DefaultMouseMap.DiffStageButton, views);
        Assert.DoesNotContain(DefaultMouseMap.DiffUnstageButton, views);
        Assert.DoesNotContain(DefaultMouseMap.DiffDiscardButton, views);
    }

    [Fact]
    public void GivenAVeryNarrowTitleRow_WhenLayingOut_ThenThereAreNoButtons()
    {
        Assert.Empty(DiffTitleButtons.Layout(DiffTitleButtons.MinimumTitleWidth));
    }

    [Fact]
    public void GivenAColumn_WhenLookingUpTheButton_ThenOnlyColumnsOnALabelHitIt()
    {
        var buttons = DiffTitleButtons.Layout(100);
        var wrap = buttons.Single(button => button.View == DefaultMouseMap.DiffWrapButton);

        Assert.Equal(DefaultMouseMap.DiffWrapButton, TitleButton.ViewAt(buttons, wrap.Start));
        Assert.Equal(DefaultMouseMap.DiffWrapButton, TitleButton.ViewAt(buttons, wrap.Start + wrap.Width - 1));
        Assert.Null(TitleButton.ViewAt(buttons, wrap.Start + wrap.Width));
        Assert.Null(TitleButton.ViewAt(buttons, 0));
    }

    [Theory]
    [InlineData(30, false)]
    [InlineData(45, false)]
    [InlineData(60, false)]
    [InlineData(80, false)]
    [InlineData(120, false)]
    [InlineData(30, true)]
    [InlineData(60, true)]
    [InlineData(80, true)]
    [InlineData(120, true)]
    public void GivenAnyWidthAndTier_WhenLayingOut_ThenHitZonesAreDisjointInsideTheRowAndMatchWhatIsDrawn(int width, bool isAscii)
    {
        var buttons = DiffTitleButtons.Layout(width);
        var glyphs = isAscii ? GraphGlyphSet.Ascii : GraphGlyphSet.Unicode;
        var title = DiffPaneRenderer.RenderTitle(new DiffPaneModel(DiffDocument.Empty, 0, 0, true), width, glyphs);

        Assert.Equal(width, TextWidth.Of(title.Text));
        if (isAscii)
        {
            Assert.All(title.Text, character => Assert.True(character < 128));
        }

        for (var index = 0; index < buttons.Count; index++)
        {
            var button = buttons[index];
            Assert.True(button.Start + button.Width <= width - 1);
            Assert.Equal(ButtonLabel.For(button.Label, isAscii), title.Text.Substring(button.Start, button.Width));
            if (index > 0)
            {
                Assert.True(button.Start > buttons[index - 1].Start + buttons[index - 1].Width);
            }
        }
    }

    [Theory]
    [InlineData(DefaultMouseMap.DiffPreviousHunkButton, 0)]
    [InlineData(DefaultMouseMap.DiffNextHunkButton, 1)]
    [InlineData(DefaultMouseMap.DiffLessContextButton, 2)]
    [InlineData(DefaultMouseMap.DiffMoreContextButton, 3)]
    [InlineData(DefaultMouseMap.DiffWholeFileButton, 4)]
    [InlineData(DefaultMouseMap.DiffIgnoreWhitespaceButton, 5)]
    [InlineData(DefaultMouseMap.DiffSideBySideButton, 6)]
    [InlineData(DefaultMouseMap.DiffWrapButton, 7)]
    [InlineData(DefaultMouseMap.DiffZoomButton, 8)]
    public void GivenAButton_WhenClickingAnyOfItsColumns_ThenTheLookupFindsItAndTheNeighbourColumnsMiss(string view, int index)
    {
        var buttons = DiffTitleButtons.Layout(100);
        var button = buttons[index];

        Assert.Equal(view, button.View);
        for (var column = button.Start; column < button.Start + button.Width; column++)
        {
            Assert.Equal(view, TitleButton.ViewAt(buttons, column));
        }

        Assert.Null(TitleButton.ViewAt(buttons, button.Start - 1));
        Assert.Null(TitleButton.ViewAt(buttons, button.Start + button.Width));
    }

    [Fact]
    public void GivenTheAsciiTier_WhenRenderingTheTitle_ThenTheButtonsUseBracketsAndPlainArrowsOfTheSameWidth()
    {
        var pane = new DiffPaneModel(DiffDocument.Empty, 0, 0, true);

        var title = DiffPaneRenderer.RenderTitle(pane, 100, GraphGlyphSet.Ascii);

        Assert.Equal(100, TextWidth.Of(title.Text));
        Assert.Contains("[< hunk]-[hunk >]-[ctx-]-[ctx+]-[file]-[-w]-[split]-[wrap]-[zoom]", title.Text, StringComparison.Ordinal);
        Assert.EndsWith("[zoom]--", title.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenADiffPane_WhenRenderingTheTitle_ThenTheButtonsEndTheRowAndTheRowKeepsItsWidth()
    {
        var pane = new DiffPaneModel(DiffDocument.Empty, 0, 0, true);

        var title = DiffPaneRenderer.RenderTitle(pane, 100, GraphGlyphSet.Unicode);

        Assert.Equal(100, TextWidth.Of(title.Text));
        Assert.EndsWith(" ‹ hunk ─ hunk › ─ ctx- ─ ctx+ ─ file ─ -w ─ split ─ wrap ─ zoom ──", title.Text, StringComparison.Ordinal);
        Assert.Contains(" Diff ", title.Text, StringComparison.Ordinal);
        Assert.Equal(9, title.Segments.Count(segment => segment.Role == SegmentRole.RepositoryTab));
    }

    [Fact]
    public void GivenAWrappedDiffWithALongPath_WhenRenderingANarrowTitle_ThenThePathIsCutBeforeTheWrapMarker()
    {
        var document = new DiffDocument(
            [new DiffRow(DiffRowKind.FileHeader, "a-very-long-directory-name/another-long-directory/file.txt", null, null, 0)],
            [],
            [0],
            [["a-very-long-directory-name/another-long-directory/file.txt"]],
            1);
        var pane = new DiffPaneModel(document, 0, 0, true, IsWrapped: true);

        var title = DiffPaneRenderer.RenderTitle(pane, 120, GraphGlyphSet.Unicode);

        Assert.Contains("[wrap]", title.Text, StringComparison.Ordinal);
        Assert.Contains(GraphGlyphSet.Unicode.Ellipsis, title.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("file.txt", title.Text, StringComparison.Ordinal);
        Assert.Equal(120, TextWidth.Of(title.Text));
    }

    [Fact]
    public void GivenAVeryWideRow_WhenLayingOut_ThenEveryButtonShowsTheKeyTheKeymapBindsInTheDiffView()
    {
        var table = DefaultKeymap.Create();
        var actions = DefaultMouseMap.CreateBindings()
            .Where(binding => binding.Gesture == MouseGesture.Click)
            .GroupBy(binding => binding.View)
            .ToDictionary(group => group.Key, group => group.First().Action);

        var buttons = DiffTitleButtons.Layout(300);

        Assert.Equal(9, buttons.Count);
        Assert.All(buttons, button =>
        {
            var binding = table.EffectiveBindings(KeyMode.Normal, DefaultKeymap.DiffView)
                .Where(candidate => candidate.Action == actions[button.View])
                .MinBy(candidate => candidate.Sequence.Length)!;
            var key = KeyFormatter.Format(binding.Sequence);
            Assert.Equal(key.Length, button.KeyLength);
            Assert.StartsWith($" {key} ", button.Label, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void GivenARowThatFitsAllButtonsOnlyWithoutKeys_WhenLayingOut_ThenTheKeysAreDroppedBeforeAnyButton()
    {
        var plain = DiffTitleButtons.Layout(100);

        Assert.Equal(9, plain.Count);
        Assert.All(plain, button => Assert.Equal(0, button.KeyLength));
    }

    [Fact]
    public void GivenARowThatFitsSomeButtonsOnlyWithoutKeys_WhenLayingOut_ThenTheSameButtonsStayWithoutKeys()
    {
        var keyed = DiffTitleButtons.Layout(300).Select(button => button.View).ToList();
        var narrow = DiffTitleButtons.Layout(50);

        Assert.All(narrow, button => Assert.Equal(0, button.KeyLength));
        Assert.All(narrow, button => Assert.Contains(button.View, keyed));
        Assert.Equal([" wrap ", " zoom "], narrow.Select(button => button.Label));
    }

    [Fact]
    public void GivenARebinding_WhenLayingOutAWideRow_ThenTheButtonShowsTheNewKey()
    {
        var bindings = DefaultKeymap.CreateBindings()
            .Select(binding => binding.Action == ActionIds.DiffToggleWrap && binding.View == DefaultKeymap.DiffView
                ? binding with { Sequence = KeySequenceParser.Parse("Ctrl-w") }
                : binding);
        var keys = new ChipKeys(DefaultKeymap.Create().WithBindings(bindings));

        var wrap = DiffTitleButtons.Layout(300, keys).Single(button => button.View == DefaultMouseMap.DiffWrapButton);

        Assert.Equal(" Ctrl-w wrap ", wrap.Label);
    }

    [Theory]
    [InlineData(WorkingTreeButtons.Unstaged, DefaultMouseMap.DiffStageButton, "<space>a")]
    [InlineData(WorkingTreeButtons.Unstaged, DefaultMouseMap.DiffDiscardButton, "<space>d")]
    [InlineData(WorkingTreeButtons.Staged, DefaultMouseMap.DiffUnstageButton, "<space>u")]
    public void GivenAWorkingTreeSide_WhenLayingOutAWideRow_ThenTheStagingButtonShowsItsKey(
        WorkingTreeButtons side, string view, string key)
    {
        var button = DiffTitleButtons.Layout(300, side).Single(candidate => candidate.View == view);

        Assert.StartsWith($" {key} ", button.Label, StringComparison.Ordinal);
        Assert.Equal(key.Length, button.KeyLength);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GivenKeyedButtons_WhenRenderingTheTitle_ThenTheKeyHasItsOwnRoleAndTheZonesMatchWhatIsDrawn(bool isAscii)
    {
        var glyphs = isAscii ? GraphGlyphSet.Ascii : GraphGlyphSet.Unicode;
        var buttons = DiffTitleButtons.Layout(300);

        var title = DiffPaneRenderer.RenderTitle(new DiffPaneModel(DiffDocument.Empty, 0, 0, true), 300, glyphs);

        Assert.Equal(300, TextWidth.Of(title.Text));
        Assert.Equal(9, title.Segments.Count(segment => segment.Role == SegmentRole.RepositoryTabKey));
        Assert.All(buttons, button =>
            Assert.Equal(ButtonLabel.For(button.Label, isAscii), title.Text.Substring(button.Start, button.Width)));
        Assert.Contains(
            title.Segments,
            segment => segment.Text == "<space>z" && segment.Role == SegmentRole.RepositoryTabKey);
    }
}
