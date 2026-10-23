using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Files;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Files;

public class PathRowLayoutTests
{
    private const string Ellipsis = "…";

    [Fact]
    public void GivenAWideRow_WhenLayingOutNameFirst_ThenTheNameIsBoldAndTheDirectoryFaintAfterIt()
    {
        var segments = PathRowLayout.Segments(FilePathStyle.NameFirst, "src/Core/Git/parser.cs", null, 60, Ellipsis);

        Assert.Equal(
            [
                new StyledSegment("parser.cs", SegmentRole.FileName),
                new StyledSegment("  src/Core/Git", SegmentRole.Faint),
            ],
            segments);
    }

    [Fact]
    public void GivenAFileInTheRoot_WhenLayingOutNameFirst_ThenOnlyTheNameShows()
    {
        var segments = PathRowLayout.Segments(FilePathStyle.NameFirst, "README.md", null, 30, Ellipsis);

        Assert.Equal([new StyledSegment("README.md", SegmentRole.FileName)], segments);
    }

    [Theory]
    [InlineData(40, "parser.cs  src/very/long/directory/path")]
    [InlineData(30, "parser.cs  …ong/directory/path")]
    [InlineData(20, "parser.cs  …ory/path")]
    [InlineData(14, "parser.cs  …th")]
    [InlineData(13, "parser.cs  …h")]
    [InlineData(12, "parser.cs")]
    [InlineData(9, "parser.cs")]
    public void GivenANarrowingRow_WhenLayingOutNameFirst_ThenTheDirectoryIsCutFromTheLeftAndDroppedBeforeTheName(
        int columns,
        string expected)
    {
        const string path = "src/very/long/directory/path/parser.cs";

        var text = PathRowLayout.Text(FilePathStyle.NameFirst, path, null, columns, Ellipsis);

        Assert.Equal(expected, text);
        Assert.True(TextWidth.Of(text) <= columns || columns < 9);
    }

    [Fact]
    public void GivenANameWiderThanTheRow_WhenLayingOutNameFirst_ThenOnlyTheNameIsReturnedForTheLineBuilderToCut()
    {
        var text = PathRowLayout.Text(FilePathStyle.NameFirst, "src/a-very-long-file-name.cs", null, 8, Ellipsis);

        Assert.Equal("a-very-long-file-name.cs", text);
    }

    [Fact]
    public void GivenAWideCharacterInTheDirectory_WhenCutting_ThenItIsNeverSplit()
    {
        var text = PathRowLayout.Text(FilePathStyle.NameFirst, "修正修正修正/a.cs", null, 9, Ellipsis);

        Assert.Equal("a.cs  …正", text);
        Assert.True(TextWidth.Of(text) <= 9);
    }

    [Fact]
    public void GivenAFolderPath_WhenLayingOutNameFirst_ThenTheTrailingSeparatorStaysWithTheName()
    {
        var text = PathRowLayout.Text(FilePathStyle.NameFirst, "src/new-folder/", null, 40, Ellipsis);

        Assert.Equal("new-folder/  src", text);
    }

    [Fact]
    public void GivenARenameInOneDirectory_WhenLayingOutNameFirst_ThenTheOldNameFollowsTheDirectory()
    {
        var text = PathRowLayout.Text(FilePathStyle.NameFirst, "src/new.cs", "src/old.cs", 40, Ellipsis);

        Assert.Equal("new.cs  src <- old.cs", text);
    }

    [Fact]
    public void GivenARenameIntoAnotherDirectory_WhenLayingOutNameFirst_ThenTheOldPathFollowsTheDirectory()
    {
        var text = PathRowLayout.Text(FilePathStyle.NameFirst, "lib/new.cs", "src/old.cs", 40, Ellipsis);

        Assert.Equal("new.cs  lib <- src/old.cs", text);
    }

    [Fact]
    public void GivenARenameInTheRoot_WhenLayingOutNameFirst_ThenTheOldNameFollowsTheName()
    {
        var text = PathRowLayout.Text(FilePathStyle.NameFirst, "new.cs", "old.cs", 40, Ellipsis);

        Assert.Equal("new.cs <- old.cs", text);
    }

    [Fact]
    public void GivenARenameAndANarrowRow_WhenLayingOutNameFirst_ThenTheOldPathGoesBeforeTheDirectoryIsCut()
    {
        var text = PathRowLayout.Text(FilePathStyle.NameFirst, "src/Core/new.cs", "src/Core/old.cs", 20, Ellipsis);

        Assert.Equal("new.cs  src/Core", text);
    }

    [Fact]
    public void GivenTheFullStyle_WhenLayingOut_ThenItIsThePathAsBeforeWhateverTheWidth()
    {
        Assert.Equal("src/a/b.cs", PathRowLayout.Text(FilePathStyle.FullPath, "src/a/b.cs", null, 3, Ellipsis));
        Assert.Equal("old.cs -> new.cs", PathRowLayout.Text(FilePathStyle.FullPath, "new.cs", "old.cs", 3, Ellipsis));
    }

    [Fact]
    public void GivenAOneColumnDirectoryThatFitsWhole_WhenLayingOutNameFirst_ThenItIsShownWithoutAnEllipsis()
    {
        Assert.Equal("very.cs  a", PathRowLayout.Text(FilePathStyle.NameFirst, "a/very.cs", null, 10, Ellipsis));
    }

    [Fact]
    public void GivenAWideCharacterThatLeavesOnlyTheEllipsis_WhenLayingOutNameFirst_ThenTheDirectoryIsDropped()
    {
        Assert.Equal("a.cs", PathRowLayout.Text(FilePathStyle.NameFirst, "修修/a.cs", null, 8, Ellipsis));
    }
}
