using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
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
