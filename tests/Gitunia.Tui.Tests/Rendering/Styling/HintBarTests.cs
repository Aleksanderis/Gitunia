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
    public void GivenHints_WhenBuildingAFooter_ThenKeysUseTheKeyRoleAndTheRestTheBarRole()
    {
        HintItem[] hints = [new("Enter", "open"), new("Esc", "back")];

        var line = OverlayBox.Hints(hints, 40, GraphGlyphSet.Unicode, "> x_");

        Assert.Contains("> x_  Enter open  Esc back", line.Text, StringComparison.Ordinal);
        Assert.Equal(
            ["Enter", "Esc"],
            line.Segments.Where(segment => segment.Role == SegmentRole.StatusKey).Select(segment => segment.Text));
    }

    [Fact]
    public void GivenAPromptPopup_WhenBuilding_ThenItsHintKeysAreDrawnAsKeys()
    {
        var prompt = PromptPopup.ForFilter("Pick", "Context", "ab", ["one", "two"], "choose");

        var frame = PromptPopupFrameBuilder.Build(prompt, 90, 24, GraphGlyphSet.Unicode)!;

        var keys = frame.Lines[^2].Segments.Where(segment => segment.Role == SegmentRole.StatusKey);
        Assert.Equal(["Ctrl-j/k", "Enter", "Esc"], keys.Select(segment => segment.Text));
    }

    [Fact]
    public void GivenAPromptWithANoteAndALabelThatLooksLikeAKey_WhenBuilding_ThenOnlyTheNamedKeysAreKeys()
    {
        var prompt = new PromptPopup(
            "Pick",
            ["Context"],
            null,
            [],
            [new(string.Empty, "Type filters"), new("Enter", "x"), new("Esc", "1/2 done")]);

        var frame = PromptPopupFrameBuilder.Build(prompt, 90, 24, GraphGlyphSet.Unicode)!;

        var keys = frame.Lines[^2].Segments.Where(segment => segment.Role == SegmentRole.StatusKey);
        Assert.Equal(["Enter", "Esc"], keys.Select(segment => segment.Text));
    }

    [Fact]
    public void GivenTheAsciiTier_WhenDrawingTheCommandsChip_ThenItIsBracketedLikeTheOtherChips()
    {
        var pieces = KeyedChip.Segments(TabBarLayout.CommandsChip(), GraphGlyphSet.Ascii, ChipStyle.Bar(isAscii: true));

        Assert.Equal("[: commands]", string.Concat(pieces.Select(piece => piece.Text)));
    }
}
