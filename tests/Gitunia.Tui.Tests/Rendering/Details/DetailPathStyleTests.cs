using Gitunia.Core.Jump.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Details;

public class DetailPathStyleTests
{
    private const string Deep = "src/Gitunia.Tui/Rendering/Details/DetailPaneRenderer.cs";

    [Fact]
    public void GivenTheNameFirstStyle_WhenRenderingAFileRow_ThenTheNameComesBeforeTheDimmedDirectory()
    {
        var line = Render(NameFirst(Deep), 1, 70);

        Assert.StartsWith("M DetailPaneRenderer.cs  src/Gitunia.Tui/Rendering", line.Text, StringComparison.Ordinal);
        Assert.Contains(
            line.Segments,
            segment => segment.Role == SegmentRole.Faint
                && segment.Text.StartsWith("  src/", StringComparison.Ordinal));
        Assert.Equal(70, TextWidth.Of(line.Text));
    }

    [Theory]
    [InlineData(60)]
    [InlineData(40)]
    [InlineData(34)]
    [InlineData(28)]
    public void GivenNarrowerRows_WhenRenderingNameFirst_ThenTheNameIsNeverCutAndTheDirectoryLosesItsStart(int width)
    {
        var line = Render(NameFirst(Deep), 1, width);

        Assert.Contains("DetailPaneRenderer.cs", line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("src/Gitunia.Tui/Rendering/Details/Detail", line.Text, StringComparison.Ordinal);
        Assert.Equal(width, TextWidth.Of(line.Text));
    }

    [Fact]
    public void GivenAnEightyColumnPaneWithoutRoomForTheDirectory_WhenRenderingNameFirst_ThenOnlyMarkAndNameShow()
    {
        var line = Render(NameFirst(Deep), 1, 24);

        Assert.Equal("M DetailPaneRenderer.cs ", line.Text);
    }

    [Fact]
    public void GivenTheFullPathStyle_WhenRenderingAFileRow_ThenItIsThePathAfterTheMark()
    {
        var content = NameFirst(Deep) with { PathStyle = FilePathStyle.FullPath };

        var line = Render(content, 1, 70);

        Assert.StartsWith($"M {Deep}", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARename_WhenRenderingNameFirst_ThenTheRowShowsTheNewNameAndTheOldOneDimmed()
    {
        var content = Content(
            FileRow("R100", "src/new.cs", "src/old.cs"),
            FilePathStyle.NameFirst);

        Assert.StartsWith("R100 new.cs  src <- old.cs", Render(content, 1, 60).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheHeadingRow_WhenRenderingNameFirst_ThenItIsUnchanged()
    {
        var line = Render(NameFirst(Deep), 0, 40);

        Assert.StartsWith("1 file changed", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenALabelOnANameFirstRow_WhenRendering_ThenTheRowShowsTheLayoutThatIsDrawnWithTheLabelOverTheMatch()
    {
        var content = NameFirst(Deep);

        var label = new JumpLabel(1, 0, 6, 'k');

        var line = DetailPaneRenderer.RenderRow(content, 1, false, 40, GraphGlyphSet.Unicode, label);

        Assert.StartsWith("M Detailk", line.Text, StringComparison.Ordinal);
        Assert.Contains("aneRenderer.cs  …", line.Text, StringComparison.Ordinal);
        Assert.Equal(40, TextWidth.Of(line.Text));
    }

    [Fact]
    public void GivenANameFirstRowThatIsCut_WhenAskingForJumpRows_ThenTheTextIsTheCutLayout()
    {
        var rows = new DetailJumpRows(NameFirst(Deep), 30, "…");

        var text = rows.TextOf(1);

        Assert.StartsWith("DetailPaneRenderer.cs  …", text, StringComparison.Ordinal);
        Assert.True(TextWidth.Of(text) <= rows.ColumnsOf(1));
    }

    [Fact]
    public void GivenAWidePane_WhenRenderingTheChangesTitle_ThenThePathChipSitsAtTheRightEndWithItsKey()
    {
        var content = NameFirst(Deep);

        var line = DetailPaneRenderer.RenderTitle(content, true, 100, GraphGlyphSet.Unicode);

        Assert.Equal(100, TextWidth.Of(line.Text));
        Assert.Contains(" <space>vv view ▾ ", line.Text, StringComparison.Ordinal);
        Assert.Contains(
            line.Segments,
            segment => segment.Role == SegmentRole.RepositoryTabKey && segment.Text == "<space>vv");
        var button = DetailPaneRenderer.ViewButton(content, 100, GraphGlyphSet.Unicode);
        Assert.Equal(DefaultMouseMap.DetailViewButton, button!.View);
        Assert.Equal(98, button.Start + button.Width);
    }

    [Fact]
    public void GivenAMediumPane_WhenRenderingTheChangesTitle_ThenTheChipDropsItsKeyBeforeItDisappears()
    {
        var content = NameFirst(Deep);

        var medium = DetailPaneRenderer.ViewButton(content, 62, GraphGlyphSet.Unicode);

        Assert.Equal(" view ▾ ", medium!.Label);
        Assert.Null(DetailPaneRenderer.ViewButton(content, 50, GraphGlyphSet.Unicode));
        Assert.Equal(50, TextWidth.Of(DetailPaneRenderer.RenderTitle(content, true, 50, GraphGlyphSet.Unicode).Text));
    }

    [Fact]
    public void GivenTheDetailsTab_WhenAskingForTheChip_ThenThereIsNone()
    {
        var content = NameFirst(Deep) with { Tab = BottomTab.Details };

        Assert.Null(DetailPaneRenderer.ViewButton(content, 120, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenTheFilesTab_WhenAskingForTheChip_ThenItIsThereWithTheKeyOfTheFilesView()
    {
        var content = NameFirst(Deep) with { Tab = BottomTab.Files };

        var button = DetailPaneRenderer.ViewButton(content, 120, GraphGlyphSet.Unicode);

        Assert.Equal(DefaultMouseMap.DetailViewButton, button!.View);
        Assert.Equal(" <space>vv view ▾ ", button.Label);
    }

    [Fact]
    public void GivenTheAsciiTier_WhenRenderingTheChip_ThenItIsBracketed()
    {
        var line = DetailPaneRenderer.RenderTitle(NameFirst(Deep), true, 100, GraphGlyphSet.Ascii);

        Assert.Contains("[<space>vv view v]", line.Text, StringComparison.Ordinal);
    }

    private static StyledLine Render(DetailContent content, int row, int width) =>
        DetailPaneRenderer.RenderRow(content.Rows[row], false, width, GraphGlyphSet.Unicode, content.PathStyle);

    private static DetailContent NameFirst(string path) =>
        Content(FileRow("M", path, null), FilePathStyle.NameFirst);

    private static DetailContent Content(DetailRow file, FilePathStyle style) => new(
        "abc1234",
        [new([new StyledSegment("1 file changed", SegmentRole.Faint)]), file],
        1,
        [],
        BottomTab.Changes,
        1)
    {
        PathStyle = style,
    };

    private static DetailRow FileRow(string mark, string path, string? oldPath) => new(
        [
            new StyledSegment(mark, SegmentRole.FileChanged),
            new StyledSegment($" {(oldPath is null ? path : $"{oldPath} -> {path}")}", SegmentRole.Plain),
        ],
        new FileRowPath(path, oldPath),
        0);
}
