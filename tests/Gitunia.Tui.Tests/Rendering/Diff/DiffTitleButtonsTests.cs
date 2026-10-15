using Gitunia.Core.Diff;
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

        Assert.Equal([" ‹ file ", " file › ", " ‹ hunk ", " hunk › ", " wrap ", " zoom "], buttons.Select(button => button.Label));
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
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(120)]
    public void GivenAnyWidth_WhenLayingOut_ThenHitZonesAreDisjointInsideTheRowAndMatchWhatIsDrawn(int width)
    {
        var buttons = DiffTitleButtons.Layout(width);
        var title = DiffPaneRenderer.RenderTitle(new DiffPaneModel(DiffDocument.Empty, "abc1234", 0, 0, true), width, GraphGlyphSet.Unicode);

        Assert.Equal(width, TextWidth.Of(title.Text));
        for (var index = 0; index < buttons.Count; index++)
        {
            var button = buttons[index];
            Assert.True(button.Start + button.Width <= width - 1);
            Assert.Equal(button.Label, title.Text.Substring(button.Start, button.Width));
            if (index > 0)
            {
                Assert.True(button.Start > buttons[index - 1].Start + buttons[index - 1].Width);
            }
        }
    }

    [Theory]
    [InlineData(DefaultMouseMap.DiffPreviousFileButton, 0)]
    [InlineData(DefaultMouseMap.DiffNextFileButton, 1)]
    [InlineData(DefaultMouseMap.DiffPreviousHunkButton, 2)]
    [InlineData(DefaultMouseMap.DiffNextHunkButton, 3)]
    [InlineData(DefaultMouseMap.DiffWrapButton, 4)]
    [InlineData(DefaultMouseMap.DiffZoomButton, 5)]
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
        var pane = new DiffPaneModel(DiffDocument.Empty, "abc1234", 0, 0, true);

        var title = DiffPaneRenderer.RenderTitle(pane, 100, GraphGlyphSet.Ascii);

        Assert.Equal(100, TextWidth.Of(title.Text));
        Assert.Contains("[< file]-[file >]-[< hunk]-[hunk >]-[wrap]-[zoom]", title.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("x", title.Text.Split("zoom]")[1], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenADiffPane_WhenRenderingTheTitle_ThenTheButtonsEndTheRowAndTheRowKeepsItsWidth()
    {
        var pane = new DiffPaneModel(DiffDocument.Empty, "abc1234", 0, 0, true);

        var title = DiffPaneRenderer.RenderTitle(pane, 100, GraphGlyphSet.Unicode);

        Assert.Equal(100, TextWidth.Of(title.Text));
        Assert.EndsWith(" ‹ file ─ file › ─ ‹ hunk ─ hunk › ─ wrap ─ zoom ──", title.Text, StringComparison.Ordinal);
        Assert.Contains(" Diff ", title.Text, StringComparison.Ordinal);
        Assert.Equal(6, title.Segments.Count(segment => segment.Role == SegmentRole.RepositoryTab));
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
        var pane = new DiffPaneModel(document, "abc1234", 0, 0, true, IsWrapped: true);

        var title = DiffPaneRenderer.RenderTitle(pane, 100, GraphGlyphSet.Unicode);

        Assert.Contains("[wrap]", title.Text, StringComparison.Ordinal);
        Assert.Contains(GraphGlyphSet.Unicode.Ellipsis, title.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("file.txt", title.Text, StringComparison.Ordinal);
        Assert.Equal(100, TextWidth.Of(title.Text));
    }
}
