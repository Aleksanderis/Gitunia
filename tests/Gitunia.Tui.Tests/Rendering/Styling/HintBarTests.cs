using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.Tabs;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Styling;

[Trait("Os", "Independent")]
public class HintBarTests
{
    [Fact]
    public void GivenKeyedEntries_WhenParsing_ThenEachSplitsIntoKeyAndLabel()
    {
        var items = HintBar.Parse("Enter choose   j/k move  Ctrl-g keep  y remove");

        Assert.Equal(
            [
                new HintItem("Enter", "choose"),
                new HintItem("j/k", "move"),
                new HintItem("Ctrl-g", "keep"),
                new HintItem("y", "remove"),
            ],
            items);
    }

    [Fact]
    public void GivenAnEntryThatStartsWithAWord_WhenParsing_ThenItIsAPlainNote()
    {
        var items = HintBar.Parse("Type filters   Up/Down select");

        Assert.Equal([new HintItem(string.Empty, "Type filters"), new HintItem("Up/Down", "select")], items);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("Esc", 1)]
    public void GivenAnEntryWithoutLabel_WhenParsing_ThenNothingThrows(string text, int count)
    {
        Assert.Equal(count, HintBar.Parse(text).Count);
    }

    [Fact]
    public void GivenHints_WhenBuildingAFooter_ThenKeysUseTheKeyRoleAndTheRestTheBarRole()
    {
        var line = OverlayBox.Hints("Enter open  Esc back", 40, GraphGlyphSet.Unicode, "> x_");

        Assert.Contains("> x_  Enter open  Esc back", line.Text, StringComparison.Ordinal);
        Assert.Equal(
            ["Enter", "Esc"],
            line.Segments.Where(segment => segment.Role == SegmentRole.StatusKey).Select(segment => segment.Text));
    }

    [Fact]
    public void GivenAPromptPopup_WhenBuilding_ThenItsHintKeysAreDrawnAsKeys()
    {
        var prompt = PromptPopup.ForFilter("Pick", "Context", "ab", ["one"], "choose");

        var frame = PromptPopupFrameBuilder.Build(prompt, 90, 24, GraphGlyphSet.Unicode)!;

        var keys = frame.Lines[^2].Segments.Where(segment => segment.Role == SegmentRole.StatusKey);
        Assert.Equal(["Up/Down", "Enter", "Esc"], keys.Select(segment => segment.Text));
    }

    [Fact]
    public void GivenTheAsciiTier_WhenDrawingTheCommandsChip_ThenItIsBracketedLikeTheOtherChips()
    {
        var pieces = KeyedChip.Segments(TabBarLayout.CommandsChip(), GraphGlyphSet.Ascii, ChipStyle.Bar(isAscii: true));

        Assert.Equal("[: commands]", string.Concat(pieces.Select(piece => piece.Text)));
    }
}
