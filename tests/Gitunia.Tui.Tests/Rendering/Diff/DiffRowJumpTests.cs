using Gitunia.Core.Diff;
using Gitunia.Core.Jump.Models;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Diff;

public class DiffRowJumpTests
{
    private static readonly GraphGlyphSet Glyphs = GraphGlyphSet.Unicode;

    [Fact]
    public void GivenAJumpLabelOnAnAddedLine_WhenRendering_ThenTheLabelSitsOverTheCharacterAfterTheMatch()
    {
        var row = new DiffRow(DiffRowKind.Added, "hello world", null, 12, 0);

        var line = DiffRowRenderer.Render(row, 40, 3, null, false, Glyphs, new JumpLabel(0, 0, 5, 'k'));

        Assert.Contains("hellokworld", line.Text, StringComparison.Ordinal);
        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.JumpLabel && segment.Text == "k");
        Assert.Equal(40, TextWidth.Of(line.Text));
    }

    [Fact]
    public void GivenAJumpLabelOnAWarningRow_WhenRendering_ThenThePrefixStaysAndTheLabelFollowsTheMatch()
    {
        var row = new DiffRow(DiffRowKind.Warning, "cut short", null, null, -1);

        var line = DiffRowRenderer.Render(row, 30, 3, null, false, Glyphs, new JumpLabel(0, 0, 3, 'k'));

        Assert.StartsWith("! cutkshort", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAJumpLabelOnAWrappedRow_WhenRenderingEachSegment_ThenOnlyTheFirstShowsIt()
    {
        var row = new DiffRow(DiffRowKind.Added, "alpha beta gamma delta epsilon zeta", null, 1, 0);
        var label = new JumpLabel(0, 0, 5, 'k');

        var first = DiffRowRenderer.RenderSegment(row, 0, 20, 3, null, false, Glyphs, label);
        var second = DiffRowRenderer.RenderSegment(row, 1, 20, 3, null, false, Glyphs, label);

        Assert.Contains(first.Segments, segment => segment.Role == SegmentRole.JumpLabel);
        Assert.DoesNotContain(second.Segments, segment => segment.Role == SegmentRole.JumpLabel);
    }

    [Fact]
    public void GivenTabsAndHeaders_WhenAskingForJumpRows_ThenTheTextIsWhatTheRowShowsAndTheColumnsExcludeThePrefix()
    {
        var rows = new DiffRow[]
        {
            new(DiffRowKind.Added, "a\tb", null, 1, 0),
            new(DiffRowKind.HunkHeader, "@@ -1 +1 @@", null, null, 0),
            new(DiffRowKind.Warning, "careful", null, null, -1),
        };
        var document = new DiffDocument(rows, [], [1], [["f"]], 1);
        var jump = new DiffJumpRows(document, 40, 3, "…");

        Assert.Equal(3, jump.Count);
        Assert.Equal("a   b", jump.TextOf(0));
        Assert.Equal(40 - ((2 * 3) + 3), jump.ColumnsOf(0));
        Assert.Equal(40, jump.ColumnsOf(1));
        Assert.Equal(38, jump.ColumnsOf(2));
        Assert.Equal(1, jump.EllipsisWidth);
    }
}
