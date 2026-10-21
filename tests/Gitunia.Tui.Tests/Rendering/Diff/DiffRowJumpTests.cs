using Gitunia.Core.Diff;
using Gitunia.Core.Jump;
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
        Assert.Equal(37, jump.ColumnsOf(1));
        Assert.Equal(38, jump.ColumnsOf(2));
        Assert.Equal(1, jump.EllipsisWidth);
    }

    [Fact]
    public void GivenASplitDocument_WhenAskingForJumpRows_ThenColumnsAreTheHalfAndRowsOutOfViewHaveNoText()
    {
        var rows = new DiffRow[]
        {
            new(DiffRowKind.Removed, "old", 1, null, 0),
            new(DiffRowKind.Added, "new", null, 1, 0),
            new(DiffRowKind.Context, "same", 2, 2, 0),
            new(DiffRowKind.HunkHeader, "@@", null, null, 0),
        };
        var document = new DiffDocument(rows, [], [], [["f"]], 2);
        var split = DiffSplitLayout.Build(document);
        var jump = new DiffJumpRows(document, 81, 3, "…", split, 0, 1);

        Assert.Equal(40 - 5, jump.ColumnsOf(0));
        Assert.Equal(40 - 5, jump.ColumnsOf(1));
        Assert.Equal(40 - 5, jump.ColumnsOf(2));
        Assert.Equal(78, jump.ColumnsOf(3));
        Assert.Equal("old", jump.TextOf(0));
        Assert.Equal("new", jump.TextOf(1));
        Assert.Equal("same", jump.TextOf(2));
        Assert.Equal(string.Empty, jump.TextOf(3));
    }

    [Fact]
    public void GivenSplitLabels_WhenRenderingAPairedLine_ThenEachSideShowsItsOwnLabelWithinItsHalf()
    {
        var document = new DiffDocument(
            [
                new DiffRow(DiffRowKind.Removed, "old needle", 1, null, 0),
                new DiffRow(DiffRowKind.Added, "new needle", null, 1, 0),
            ],
            [],
            [],
            [["f"]],
            1);
        var line = DiffSplitLayout.Build(document).Lines[0];

        var rendered = DiffSplitRenderer.Render(
            document,
            line,
            0,
            81,
            3,
            null,
            false,
            false,
            Glyphs,
            new JumpLabel(0, 4, 6, 'a'),
            new JumpLabel(1, 4, 6, 'b'));

        Assert.Contains("old needlea", rendered.Text, StringComparison.Ordinal);
        Assert.Contains("new needleb", rendered.Text, StringComparison.Ordinal);
        Assert.Equal(2, rendered.Segments.Count(segment => segment.Role == SegmentRole.JumpLabel));
        Assert.Equal(81, TextWidth.Of(rendered.Text));
    }

    [Fact]
    public void GivenAMatchNearTheEdgeOfAHalf_WhenLabelling_ThenOnlyTheSideWithRoomGetsALetter()
    {
        // A half is 40 columns, 5 of them prefix: 35 for text. A label takes the column after the match.
        var oldText = new string('x', 28) + "needle";
        var newText = new string('x', 29) + "needle";
        var document = new DiffDocument(
            [
                new DiffRow(DiffRowKind.Removed, oldText, 1, null, 0),
                new DiffRow(DiffRowKind.Added, newText, null, 1, 0),
            ],
            [],
            [],
            [["f"]],
            1);
        var rows = new DiffJumpRows(document, 81, 3, "…", DiffSplitLayout.Build(document), 0, 0);

        var labels = QuickJumpLabeler.Compute("needle", rows, new JumpWindow(0, 2, 0));

        Assert.True(labels.Single(label => label.Row == 0).HasLetter);
        Assert.False(labels.Single(label => label.Row == 1).HasLetter);
    }
}
