using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Diff;

[Trait("Os", "Independent")]
public class ChangeGroupButtonsTests
{
    private const int Padding = 1;

    [Fact]
    public void GivenAWideRowOnTheUnstagedSide_WhenLayingOut_ThenStageAndDiscardEndAtTheRightEdgeOfTheText()
    {
        var buttons = ChangeGroupButtons.Layout(80, Padding, WorkingTreeButtons.Unstaged);

        Assert.Equal(
            [DefaultMouseMap.DiffStageButton, DefaultMouseMap.DiffDiscardButton],
            buttons.Select(button => button.View));
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
    [InlineData(0, 2)]
    [InlineData(31, 2)]
    [InlineData(60, 2)]
    [InlineData(61, 1)]
    [InlineData(70, 1)]
    [InlineData(71, 0)]
    [InlineData(78, 0)]
    public void GivenALongLine_WhenLayingOut_ThenTheButtonsNeverCoverItsText(int contentWidth, int expected)
    {
        var buttons = ChangeGroupButtons.Layout(78, Padding, WorkingTreeButtons.Unstaged, null, contentWidth);

        Assert.Equal(expected, buttons.Count);
        foreach (var button in buttons)
        {
            Assert.True(button.Start - 1 - Padding >= contentWidth);
        }
    }

    [Fact]
    public void GivenTrailingBlanksAndWideCharacters_WhenMeasuringContent_ThenOnlyTheTextCounts()
    {
        var line = new StyledLine(
        [
            new StyledSegment("ab", SegmentRole.Plain),
            new StyledSegment("\u4e2d  ", SegmentRole.Plain),
        ]);

        Assert.Equal(4, ChangeGroupButtons.ContentWidth(line));
    }

    [Theory]
    [InlineData(80, 2)]
    [InlineData(48, 2)]
    [InlineData(47, 1)]
    [InlineData(38, 1)]
    [InlineData(37, 0)]
    [InlineData(0, 0)]
    public void GivenANarrowRow_WhenLayingOut_ThenDiscardGoesFirstAndThirtyTextColumnsStay(int textWidth, int expected)
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
    public void GivenWideCharacters_WhenOverlayingButtons_ThenTheLineKeepsItsWidthAndTheChipsSitAtTheirColumns()
    {
        const int textWidth = 61;
        var text = string.Concat(Enumerable.Repeat("漢", textWidth / 2)) + "x";
        var line = PanePadding.Wrap(new StyledLine([new StyledSegment(text, SegmentRole.Plain)]));
        var buttons = ChangeGroupButtons.Layout(textWidth, Padding, WorkingTreeButtons.Unstaged);

        var result = ChangeGroupButtons.Overlay(line, buttons, Padding, GraphGlyphSet.Unicode);

        Assert.Equal(textWidth + 2 * Padding, TextWidth.Of(result.Text));
        Assert.EndsWith(" stage   discard  ", result.Text);
        var before = result.Text[..result.Text.IndexOf(" stage ", StringComparison.Ordinal)];
        Assert.Equal(buttons[0].Start, TextWidth.Of(before));
    }

    [Fact]
    public void GivenACutThroughAWideCharacter_WhenOverlayingButtons_ThenTheHalfBecomesASpace()
    {
        const int textWidth = 60;
        var text = string.Concat(Enumerable.Repeat("漢", textWidth / 2));
        var line = PanePadding.Wrap(new StyledLine([new StyledSegment(text, SegmentRole.Plain)]));
        var buttons = ChangeGroupButtons.Layout(textWidth, Padding, WorkingTreeButtons.Staged);

        var result = ChangeGroupButtons.Overlay(line, buttons, Padding, GraphGlyphSet.Unicode);

        Assert.Equal(textWidth + 2 * Padding, TextWidth.Of(result.Text));
        Assert.DoesNotContain('�', result.Text);
    }

    [Fact]
    public void GivenAnAsciiTier_WhenOverlayingButtons_ThenTheChipsUseBrackets()
    {
        var plain = new StyledLine([new StyledSegment(new string('x', 45), SegmentRole.Plain)]);
        var line = PanePadding.Wrap(plain);
        var buttons = ChangeGroupButtons.Layout(45, Padding, WorkingTreeButtons.Staged);

        var result = ChangeGroupButtons.Overlay(line, buttons, Padding, GraphGlyphSet.Ascii);

        Assert.Contains("[unstage]", result.Text);
        Assert.Equal(47, TextWidth.Of(result.Text));
    }

    [Fact]
    public void GivenARowWideEnoughForKeys_WhenLayingOut_ThenEachButtonShowsItsKeyFromTheKeymap()
    {
        var buttons = ChangeGroupButtons.Layout(80, Padding, WorkingTreeButtons.Unstaged);

        Assert.Equal([" <space>a stage ", " <space>d discard "], buttons.Select(button => button.Label));
        Assert.All(buttons, button => Assert.True(button.KeyLength > 0));
        Assert.Equal(Padding + 80, buttons[^1].Start + buttons[^1].Width);
    }

    [Fact]
    public void GivenAStagedRowWideEnoughForKeys_WhenLayingOut_ThenUnstageShowsItsKey()
    {
        var buttons = ChangeGroupButtons.Layout(80, Padding, WorkingTreeButtons.Staged);

        Assert.Equal(" <space>u unstage ", buttons.Single().Label);
    }

    [Theory]
    [InlineData(66, 2, 2)]
    [InlineData(65, 2, 0)]
    [InlineData(50, 2, 0)]
    [InlineData(46, 1, 0)]
    public void GivenARowThatFitsTheButtonsOnlyPlain_WhenLayingOut_ThenTheKeysGoBeforeAnyButton(
        int textWidth, int count, int keyed)
    {
        var buttons = ChangeGroupButtons.Layout(textWidth, Padding, WorkingTreeButtons.Unstaged);

        Assert.Equal(count, buttons.Count);
        Assert.Equal(keyed, buttons.Count(button => button.KeyLength > 0));
    }

    [Fact]
    public void GivenARebinding_WhenLayingOut_ThenTheGroupButtonShowsTheNewKey()
    {
        var bindings = DefaultKeymap.CreateBindings()
            .Select(Rebound);
        var keys = new ChipKeys(DefaultKeymap.Create().WithBindings(bindings));

        var buttons = ChangeGroupButtons.Layout(80, Padding, WorkingTreeButtons.Unstaged, keys);

        Assert.Equal(" Ctrl-s stage ", buttons[0].Label);
    }

    private static KeyBinding Rebound(KeyBinding binding)
    {
        if (binding.Action == ActionIds.WorkingTreeStage
            && binding.View == DefaultKeymap.WorkingUnstagedDiffView)
        {
            return binding with { Sequence = KeySequenceParser.Parse("Ctrl-s") };
        }

        return binding;
    }
}
