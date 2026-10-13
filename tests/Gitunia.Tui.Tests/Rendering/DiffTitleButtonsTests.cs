using Gitunia.Core.Diff;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

public class DiffTitleButtonsTests
{
    [Fact]
    public void GivenAWideTitleRow_WhenLayingOut_ThenEveryButtonIsShownInKeyOrderAtTheRightEdge()
    {
        var buttons = DiffTitleButtons.Layout(80);

        Assert.Equal(["[<f]", "[f>]", "[<h]", "[h>]", "[w]", "[z]", "[x]"], buttons.Select(button => button.Label));
        Assert.Equal(78, buttons[^1].Start + buttons[^1].Width);
        Assert.Equal(1, buttons[1].Start - (buttons[0].Start + buttons[0].Width));
    }

    [Fact]
    public void GivenANarrowTitleRow_WhenLayingOut_ThenOnlyTheMostUsefulButtonsRemainAndTheTitleKeepsItsWidth()
    {
        var buttons = DiffTitleButtons.Layout(44);

        Assert.Equal(["[w]", "[z]", "[x]"], buttons.Select(button => button.Label));
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
        var buttons = DiffTitleButtons.Layout(80);
        var wrap = buttons.Single(button => button.View == DefaultMouseMap.DiffWrapButton);

        Assert.Equal(DefaultMouseMap.DiffWrapButton, TitleButton.ViewAt(buttons, wrap.Start));
        Assert.Equal(DefaultMouseMap.DiffWrapButton, TitleButton.ViewAt(buttons, wrap.Start + wrap.Width - 1));
        Assert.Null(TitleButton.ViewAt(buttons, wrap.Start + wrap.Width));
        Assert.Null(TitleButton.ViewAt(buttons, 0));
    }

    [Fact]
    public void GivenADiffPane_WhenRenderingTheTitle_ThenTheButtonsEndTheRowAndTheRowKeepsItsWidth()
    {
        var pane = new DiffPaneModel(DiffDocument.Empty, "abc1234", 0, 0, true);

        var title = DiffPaneRenderer.RenderTitle(pane, 80, GraphGlyphSet.Unicode);

        Assert.Equal(80, TextWidth.Of(title.Text));
        Assert.EndsWith("[<f] [f>] [<h] [h>] [w] [z] [x] ─", title.Text, StringComparison.Ordinal);
        Assert.Contains(" Diff ", title.Text, StringComparison.Ordinal);
        Assert.Equal(7, title.Segments.Count(segment => segment.Role == SegmentRole.Button));
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

        var title = DiffPaneRenderer.RenderTitle(pane, 80, GraphGlyphSet.Unicode);

        Assert.Contains("[wrap]", title.Text, StringComparison.Ordinal);
        Assert.Contains(GraphGlyphSet.Unicode.Ellipsis, title.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("file.txt", title.Text, StringComparison.Ordinal);
        Assert.Equal(80, TextWidth.Of(title.Text));
    }
}
