using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Diff;

[Trait("Os", "Independent")]
public class ChangeGroupMarkerTests
{
    [Theory]
    [InlineData(true, "│")]
    [InlineData(false, "|")]
    public void GivenAPaddedLine_WhenMarkingIt_ThenTheLeftMarginHoldsTheBarAndTheWidthIsKept(bool unicode, string bar)
    {
        var line = PanePadding.Wrap(new LineBuilder(6, "…").Append("+ text", SegmentRole.DiffAdded).Build(), 2);

        var marked = ChangeGroupMarker.Mark(line, GraphGlyphSet.For(unicode));

        Assert.Equal($"{bar} + text  ", marked.Text);
        Assert.Equal(line.Text.Length, marked.Text.Length);
        Assert.Equal(SegmentRole.DiffGroupBar, marked.Segments[0].Role);
        Assert.All(marked.Segments, segment => Assert.True(segment.InGroup));
    }
}
