using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

public class MainlineFrameBuilderTests
{
    private const string Title = "Revert merge commit abc: keep the mainline of";

    private static readonly string[] Parents = ["aaaaaaa main two", "bbbbbbb topic two"];

    [Fact]
    public void GivenTwoParents_WhenBuilding_ThenEachRowShowsItsParentAndDigit()
    {
        var frame = MainlineFrameBuilder.Build(Title, Parents, 80, 24, GraphGlyphSet.Unicode);

        Assert.NotNull(frame);
        Assert.Equal(5, frame.Lines.Count);
        Assert.Contains("aaaaaaa main two", frame.Lines[1].Text, StringComparison.Ordinal);
        Assert.Contains("[1]", frame.Lines[1].Text, StringComparison.Ordinal);
        Assert.Contains("bbbbbbb topic two", frame.Lines[2].Text, StringComparison.Ordinal);
        Assert.Contains("[2]", frame.Lines[2].Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, null)]
    public void GivenTwoParents_WhenAskingForAFrameLine_ThenOnlyTheParentRowsAnswer(int line, int? expected)
    {
        Assert.Equal(expected, MainlineFrameBuilder.ParentAt(line, 2));
    }

    [Fact]
    public void GivenATinyScreen_WhenBuilding_ThenThereIsNoOverlay()
    {
        Assert.Null(MainlineFrameBuilder.Build(Title, Parents, 10, 24, GraphGlyphSet.Unicode));
        Assert.Null(MainlineFrameBuilder.Build(Title, Parents, 80, 6, GraphGlyphSet.Unicode));
    }
}
