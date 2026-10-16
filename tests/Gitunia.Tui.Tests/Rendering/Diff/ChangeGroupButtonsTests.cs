using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Diff;

public class ChangeGroupButtonsTests
{
    private const int Padding = 1;

    [Fact]
    public void GivenAWideRowOnTheUnstagedSide_WhenLayingOut_ThenStageAndDiscardEndAtTheRightEdgeOfTheText()
    {
        var buttons = ChangeGroupButtons.Layout(80, Padding, WorkingTreeButtons.Unstaged);

        Assert.Equal([DefaultMouseMap.DiffStageButton, DefaultMouseMap.DiffDiscardButton], buttons.Select(button => button.View));
        var last = buttons[^1];
        Assert.Equal(Padding + 80, last.Start + last.Width);
        Assert.Equal(buttons[0].Start + buttons[0].Width + 1, last.Start);
    }

    [Fact]
    public void GivenTheStagedSide_WhenLayingOut_ThenOnlyUnstageIsOffered()
    {
        var buttons = ChangeGroupButtons.Layout(80, Padding, WorkingTreeButtons.Staged);

        Assert.Equal([DefaultMouseMap.DiffUnstageButton], buttons.Select(button => button.View));
    }

    [Fact]
    public void GivenNoStagingSide_WhenLayingOut_ThenThereAreNoButtons()
    {
        Assert.Empty(ChangeGroupButtons.Layout(80, Padding, WorkingTreeButtons.None));
    }

    [Theory]
    [InlineData(80, 2)]
    [InlineData(48, 2)]
    [InlineData(47, 1)]
    [InlineData(38, 1)]
    [InlineData(37, 0)]
    [InlineData(0, 0)]
    public void GivenANarrowRow_WhenLayingOut_ThenDiscardGoesFirstAndThirtyColumnsOfTextStay(int textWidth, int expected)
    {
        var buttons = ChangeGroupButtons.Layout(textWidth, Padding, WorkingTreeButtons.Unstaged);

        Assert.Equal(expected, buttons.Count);
        if (buttons.Count > 0)
        {
            Assert.True(buttons[0].Start - 1 - Padding >= ChangeGroupButtons.MinimumTextWidth);
            Assert.Equal(DefaultMouseMap.DiffStageButton, buttons[0].View);
        }
    }

    [Fact]
    public void GivenALineWithWideCharacters_WhenOverlayingButtons_ThenTheLineKeepsItsWidthAndTheChipsSitAtTheirColumns()
    {
        const int textWidth = 61;
        var text = string.Concat(Enumerable.Repeat("漢", textWidth / 2)) + "x";
        var line = PanePadding.Wrap(new StyledLine([new StyledSegment(text, SegmentRole.Plain)]), Padding);
        var buttons = ChangeGroupButtons.Layout(textWidth, Padding, WorkingTreeButtons.Unstaged);

        var result = ChangeGroupButtons.Overlay(line, buttons, Padding, GraphGlyphSet.Unicode);

        Assert.Equal(textWidth + 2 * Padding, TextWidth.Of(result.Text));
        Assert.EndsWith(" stage   discard  ", result.Text);
        Assert.Equal(buttons[0].Start, TextWidth.Of(result.Text[..result.Text.IndexOf(" stage ", StringComparison.Ordinal)]));
    }

    [Fact]
    public void GivenACutThroughAWideCharacter_WhenOverlayingButtons_ThenTheHalfBecomesASpace()
    {
        const int textWidth = 60;
        var text = string.Concat(Enumerable.Repeat("漢", textWidth / 2));
        var line = PanePadding.Wrap(new StyledLine([new StyledSegment(text, SegmentRole.Plain)]), Padding);
        var buttons = ChangeGroupButtons.Layout(textWidth, Padding, WorkingTreeButtons.Staged);

        var result = ChangeGroupButtons.Overlay(line, buttons, Padding, GraphGlyphSet.Unicode);

        Assert.Equal(textWidth + 2 * Padding, TextWidth.Of(result.Text));
        Assert.DoesNotContain('�', result.Text);
    }

    [Fact]
    public void GivenAnAsciiTier_WhenOverlayingButtons_ThenTheChipsUseBrackets()
    {
        var line = PanePadding.Wrap(new StyledLine([new StyledSegment(new string('x', 60), SegmentRole.Plain)]), Padding);
        var buttons = ChangeGroupButtons.Layout(60, Padding, WorkingTreeButtons.Staged);

        var result = ChangeGroupButtons.Overlay(line, buttons, Padding, GraphGlyphSet.Ascii);

        Assert.Contains("[unstage]", result.Text);
        Assert.Equal(62, TextWidth.Of(result.Text));
    }
}
