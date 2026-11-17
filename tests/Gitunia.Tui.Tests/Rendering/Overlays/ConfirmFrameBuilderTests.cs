using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
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
        new("Wide", [new string('中', 50)], "x", "Go"),
    ];

    [Fact]
    public void GivenAnUndoHint_WhenBuilding_ThenItIsPlainAndAWarningStaysInTheErrorColour()
    {
        var undo = new ConfirmPrompt("Mark", ["Mark a.txt as resolved?"], UndoNotes.FromHistory, "Mark");
        var warning = new ConfirmPrompt("Delete", ["Delete x?"], "Cannot be undone.", "Delete");

        var undoFrame = ConfirmFrameBuilder.Build(undo, 100, 30, GraphGlyphSet.Unicode)!;
        var warningFrame = ConfirmFrameBuilder.Build(warning, 100, 30, GraphGlyphSet.Unicode)!;

        Assert.DoesNotContain(Segments(undoFrame), segment => segment.Role == SegmentRole.Error);
        Assert.Contains(Segments(warningFrame), segment => segment.Role == SegmentRole.Error);
        Assert.True(UndoNotes.IsUndoNote(UndoNotes.FromReflog));
        Assert.False(UndoNotes.IsUndoNote("Cannot be undone."));
    }

    [Fact]
    public void GivenAWarningThatContainsAnUndoNote_WhenBuilding_ThenItStaysInTheErrorColour()
    {
        var mixed = new ConfirmPrompt(
            "Rebase",
            ["Rebase x onto y?"],
            $"A stop on conflicts leaves a rebase in progress. {UndoNotes.FromReflog}",
            "Rebase");

        var frame = ConfirmFrameBuilder.Build(mixed, 100, 30, GraphGlyphSet.Unicode)!;

        Assert.Contains(Segments(frame), segment => segment.Role == SegmentRole.Error);
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
