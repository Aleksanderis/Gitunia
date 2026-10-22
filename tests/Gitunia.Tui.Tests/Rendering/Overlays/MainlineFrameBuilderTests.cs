using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

public class MainlineFrameBuilderTests
{
    private const string Title = "Revert merge commit abc: keep the mainline of";

    private static readonly string[] Parents = ["aaaaaaa main two", "bbbbbbb topic two"];

    [Fact]
    public void GivenTwoParents_WhenBuilding_ThenEachRowShowsItsParentAndDigit()
    {
        var frame = MainlineFrameBuilder.Build(Title, Parents, 80, 24, GraphGlyphSet.Unicode, 2);

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
    public void GivenDifferentScreenSizes_WhenBuilding_ThenTheFrameIsCenteredOnEach()
    {
        var before = MainlineFrameBuilder.Build(Title, Parents, 80, 24, GraphGlyphSet.Unicode, 2);
        var wider = MainlineFrameBuilder.Build(Title, Parents, 120, 40, GraphGlyphSet.Unicode, 2);
        var narrow = MainlineFrameBuilder.Build(Title, Parents, 40, 24, GraphGlyphSet.Unicode, 2);

        Assert.NotNull(before);
        Assert.NotNull(wider);
        Assert.NotNull(narrow);
        Assert.Equal((80 - 72) / 2, before.Column);
        Assert.Equal((120 - 72) / 2, wider.Column);
        Assert.Equal((40 - 38) / 2, narrow.Column);
        Assert.Equal((40 - 5) / 2, wider.Row);
    }

    [Fact]
    public void GivenAnUnavailableParent_WhenBuilding_ThenItsRowIsMarkedAndDimmed()
    {
        var frame = MainlineFrameBuilder.Build(Title, Parents, 80, 24, GraphGlyphSet.Unicode, 1);

        Assert.NotNull(frame);
        Assert.DoesNotContain("unavailable", frame.Lines[1].Text, StringComparison.Ordinal);
        Assert.Contains("[2] unavailable", frame.Lines[2].Text, StringComparison.Ordinal);
        var marked = frame.Lines[2].Segments.Where(s => s.Text.Contains("[2]", StringComparison.Ordinal));
        Assert.All(marked, s => Assert.Equal(SegmentRole.Faint, s.Role));
    }

    [Fact]
    public void GivenALongSubjectOnANarrowScreen_WhenBuilding_ThenTheUnavailableMarkSurvives()
    {
        string[] parents = ["aaaaaaa main two", "bbbbbbb " + new string('x', 120)];

        var frame = MainlineFrameBuilder.Build(Title, parents, 50, 24, GraphGlyphSet.Unicode, 1);

        Assert.NotNull(frame);
        Assert.Contains("[2] unavailable", frame.Lines[2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenATinyScreen_WhenBuilding_ThenThereIsNoOverlay()
    {
        Assert.Null(MainlineFrameBuilder.Build(Title, Parents, 10, 24, GraphGlyphSet.Unicode, 2));
        Assert.Null(MainlineFrameBuilder.Build(Title, Parents, 80, 6, GraphGlyphSet.Unicode, 2));
    }

    [Fact]
    public void GivenALongParentAtWidth120_WhenBuilding_ThenItIsShownInFullAndTheFrameGrows()
    {
        string[] parents = ["aaaaaaa " + new string('x', 70), "bbbbbbb topic two"];

        var frame = MainlineFrameBuilder.Build(Title, parents, 120, 40, GraphGlyphSet.Unicode, 2)!;

        Assert.Contains(frame.Lines, line => line.Text.Contains(parents[0], StringComparison.Ordinal));
        Assert.True(frame.Width > 72 && frame.Width <= 96);
    }
}
