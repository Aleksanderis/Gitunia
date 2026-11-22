using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

[Trait("Os", "Independent")]
public class ConfirmFrameBuilderTests
{
    private static readonly ConfirmPrompt[] Prompts =
    [
        new("Delete", ["Delete branch x?"], "Cannot be undone.", "Delete"),
        new("Discard", [string.Join(' ', Enumerable.Repeat("word", 60)), "Second paragraph."], "Gone for good.", "Discard"),
        new("Plain", ["Mark a.txt as resolved?"], string.Empty, "Mark"),
        new("Wide", [new string('中', 50)], "x", "Go"),
    ];

    [Fact]
    public void GivenAWarning_WhenBuilding_ThenItIsInTheErrorColour()
    {
        var warning = new ConfirmPrompt("Delete", ["Delete x?"], "Cannot be undone.", "Delete");

        var frame = ConfirmFrameBuilder.Build(warning, 100, 30, GraphGlyphSet.Unicode)!;

        Assert.Contains(Segments(frame), segment => segment.Role == SegmentRole.Error);
    }

    [Fact]
    public void GivenNoConsequence_WhenBuilding_ThenThereIsNoWarningRowAndOneBlankRowLess()
    {
        var plain = new ConfirmPrompt("Mark", ["Mark a.txt as resolved?"], string.Empty, "Mark");
        var warned = plain with { Consequence = "Cannot be undone." };

        var plainFrame = ConfirmFrameBuilder.Build(plain, 100, 30, GraphGlyphSet.Unicode)!;
        var warnedFrame = ConfirmFrameBuilder.Build(warned, 100, 30, GraphGlyphSet.Unicode)!;

        Assert.DoesNotContain(Segments(plainFrame), segment => segment.Role == SegmentRole.Error);
        Assert.Equal(warnedFrame.Lines.Count - 2, plainFrame.Lines.Count);
    }

    [Fact]
    public void GivenAnyScreenSize_WhenBuilding_ThenTheFrameExistsExactlyWhenTheSharedLayoutSaysItFits()
    {
        foreach (var prompt in Prompts)
        {
            for (var width = 0; width <= 130; width += 7)
            {
                for (var height = 0; height <= 40; height++)
                {
                    var frame = ConfirmFrameBuilder.Build(prompt, width, height, GraphGlyphSet.Unicode);

                    Assert.True(
                        ConfirmLayout.Fits(prompt, width, height) == (frame is not null),
                        $"{prompt.Title} at {width}x{height}");
                }
            }
        }
    }

    private static IEnumerable<StyledSegment> Segments(OverlayFrame frame) =>
        frame.Lines.SelectMany(line => line.Segments);
}
