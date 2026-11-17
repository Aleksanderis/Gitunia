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

        Assert.DoesNotContain(undoFrame.Lines.SelectMany(line => line.Segments), segment => segment.Role == SegmentRole.Error);
        Assert.Contains(warningFrame.Lines.SelectMany(line => line.Segments), segment => segment.Role == SegmentRole.Error);
        Assert.True(UndoNotes.IsUndoNote(UndoNotes.FromReflog));
        Assert.False(UndoNotes.IsUndoNote("Cannot be undone."));
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
}
