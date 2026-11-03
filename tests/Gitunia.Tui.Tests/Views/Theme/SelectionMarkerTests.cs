using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views.Theme;
using Xunit;

namespace Gitunia.Tui.Tests.Views.Theme;

[Trait("Os", "Independent")]
public class SelectionMarkerTests
{
    [Fact]
    public void GivenOneSegment_WhenMarkingTheMiddle_ThenItSplitsIntoThreeAndOnlyTheMiddleIsInTheRange()
    {
        var line = new StyledLine([new StyledSegment("abcdef", SegmentRole.Plain)]);

        var marked = SelectionMarker.Mark(line, 2, 4);

        Assert.Equal(["ab", "cd", "ef"], marked.Segments.Select(segment => segment.Text));
        Assert.Equal(
            [SegmentSelection.Inherit, SegmentSelection.Range, SegmentSelection.Inherit],
            marked.Segments.Select(segment => segment.Selection));
        Assert.Equal("abcdef", marked.Text);
    }

    [Fact]
    public void GivenSeveralSegments_WhenMarkingAcrossThem_ThenEachKeepsItsRoleAndTheColumnsAreCountedAcrossThem()
    {
        var line = new StyledLine(
            [new StyledSegment("12 ", SegmentRole.Faint), new StyledSegment("hello", SegmentRole.DiffAdded)]);

        var marked = SelectionMarker.Mark(line, 3, 6);

        Assert.Equal(["12 ", "hel", "lo"], marked.Segments.Select(segment => segment.Text));
        Assert.Equal(SegmentRole.Faint, marked.Segments[0].Role);
        Assert.Equal(SegmentSelection.Range, marked.Segments[1].Selection);
        Assert.Equal(SegmentRole.DiffAdded, marked.Segments[2].Role);
    }

    [Fact]
    public void GivenAWideCharacterOnTheEdge_WhenMarking_ThenItIsNeverSplit()
    {
        var line = new StyledLine([new StyledSegment("a中b", SegmentRole.Plain)]);

        var marked = SelectionMarker.Mark(line, 2, 3);

        Assert.Equal(["a", "中", "b"], marked.Segments.Select(segment => segment.Text));
        Assert.Equal(SegmentSelection.Range, marked.Segments[1].Selection);
    }

    [Fact]
    public void GivenAnEmptyStretch_WhenMarking_ThenTheLineIsUnchanged()
    {
        var line = new StyledLine([new StyledSegment("abc", SegmentRole.Plain)]);

        Assert.Same(line, SelectionMarker.Mark(line, 2, 2));
    }
}
