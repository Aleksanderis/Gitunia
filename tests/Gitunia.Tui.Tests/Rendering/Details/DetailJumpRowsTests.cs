using Gitunia.Core.Jump;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Details;

[Trait("Os", "Independent")]
public class DetailJumpRowsTests
{
    [Fact]
    public void GivenTheChangesTab_WhenAskingForJumpRows_ThenAFileRowIsItsPathAndTheHeadingIsItsText()
    {
        var rows = new DetailJumpRows(Changes(), 20, "…");

        Assert.Equal(3, rows.Count);
        Assert.Equal("2 files changed", rows.TextOf(0));
        Assert.Equal("src/parser.cs", rows.TextOf(1));
        Assert.Equal(20, rows.ColumnsOf(0));
        Assert.Equal(18, rows.ColumnsOf(1));
        Assert.Equal(15, rows.ColumnsOf(2));
    }

    [Fact]
    public void GivenALongPath_WhenLabelling_ThenAMatchPastThePaneWidthTakesNoLetter()
    {
        var rows = new DetailJumpRows(Changes(), 20, "…");

        var labels = QuickJumpLabeler.Compute("target", rows, new JumpWindow(0, 3, 0));

        Assert.Equal(2, labels.Single().Row);
        Assert.False(labels.Single().HasLetter);
        Assert.True(QuickJumpLabeler.Compute("parser", rows, new JumpWindow(0, 3, 0)).Single().HasLetter);
    }

    [Fact]
    public void GivenALabel_WhenRenderingAFileRow_ThenTheMarkStaysAndTheLabelSitsAfterTheMatch()
    {
        var line = DetailPaneRenderer.RenderRow(Changes(), 1, false, 30, GraphGlyphSet.Unicode, new JumpLabel(1, 4, 6, 'k'));

        Assert.StartsWith("M src/parserk", line.Text, StringComparison.Ordinal);
        Assert.Equal(30, TextWidth.Of(line.Text));
    }

    [Fact]
    public void GivenNoRows_WhenAskingForJumpRows_ThenThereAreNone()
    {
        var rows = new DetailJumpRows(DetailContent.Empty, 20, "…");

        Assert.Equal(0, rows.Count);
        Assert.Empty(QuickJumpLabeler.Compute("x", rows, new JumpWindow(0, 5, 0)));
    }

    private static DetailContent Changes() => new(
        "abc1234",
        [
            new([new StyledSegment("2 files changed", SegmentRole.Faint)]),
            FileRow("M", "src/parser.cs"),
            FileRow("R100", "a/very/deep/directory/structure/target.cs"),
        ],
        1,
        [],
        BottomTab.Changes);

    private static DetailRow FileRow(string mark, string path) =>
        new(
            [new StyledSegment(mark, SegmentRole.FileChanged), new StyledSegment($" {path}", SegmentRole.Plain)],
            null,
            0);
}
